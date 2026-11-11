namespace Daoris.Driver;

/// <summary>
/// A run's review read by its run alone (<see cref="RunReview"/>), from the run's binding, its node record and its review records, so it
/// reads the same after the run's trees and sessions are gone.
/// </summary>
public static class RunReviews
{
    /// <summary>
    /// <paramref name="run"/>'s review at <paramref name="attempt"/>, or at its newest record; null where the run is not bound to a graph that
    /// holds a review.
    /// </summary>
    public static RunReview? Read(string home, string run, int? attempt)
    {
        var records = new NodeRecordFiles(home, () => false);
        if (new GraphRuns(records).Find(run.Trim().TrimStart('#')) is not { Version.Graph: { } graph } bound) return null;
        if (graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Review) is not { } review) return null;

        var record = records.Read(bound.Unit) ?? new NodeRecord(bound.Unit);
        var status = WorkPerformer.StatusOf(record, review.Id);
        var kept = ReviewRecords.Of(home, bound.Run).Where(each => each.Node == review.Id).ToList();
        var read = attempt is { } named ? kept.FirstOrDefault(each => each.Attempt == named) : kept.LastOrDefault();
        var newest = kept.LastOrDefault();
        var standing = read is not null && status.State == NodeStates.Active && status.Attempt == read.Attempt;
        return new RunReview(bound.Run, review.Id, read)
        {
            State = status.State, Standing = status.Attempt, Since = status.Since, By = status.By, Words = status.Words,
            Presses = standing ? [NodeWordActs.Accept, NodeWordActs.Changes, NodeWordActs.Stop] : [],
            Newer = read is not null && newest is not null && newest.Attempt > read.Attempt
                ? new RunReviewNewer(newest.Attempt, newest.Revision, Changed(read, newest)) : null,
            Follows = [.. Following(graph, review.Id).Select(node => Step(record, node))],
            Ended = record.Ended?.Outcome,
        };
    }

    /// <summary>The paths whose content differs between two records: a row added, dropped or written to other content.</summary>
    public static IReadOnlyList<string> Changed(ReviewRecord from, ReviewRecord to)
    {
        var before = from.Rows.ToDictionary(row => row.Path, row => (row.Change, row.After), StringComparer.Ordinal);
        var after = to.Rows.ToDictionary(row => row.Path, row => (row.Change, row.After), StringComparer.Ordinal);
        return [.. before.Keys.Union(after.Keys, StringComparer.Ordinal)
            .Where(path => !before.TryGetValue(path, out var was) || !after.TryGetValue(path, out var now) || was != now)
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>The nodes an Accept may lead to, nearest first: a way back on the review's changes leads to none.</summary>
    private static List<NodeDeclaration> Following(NodeGraph graph, string review)
    {
        var found = new List<string>();
        var next = new Queue<string>([review]);
        while (next.TryDequeue(out var from))
        {
            foreach (var wire in graph.Wires.Where(wire => wire.From?.Node == from && wire.Kind is not (WireKinds.Back or WireKinds.Rejected)))
            {
                if (wire.To.Node is { } to && to != review && !found.Contains(to, StringComparer.Ordinal))
                {
                    found.Add(to);
                    next.Enqueue(to);
                }
            }
        }

        return [.. found.Select(id => graph.Nodes.FirstOrDefault(node => node.Id == id)).OfType<NodeDeclaration>()];
    }

    private static RunReviewStep Step(NodeRecord record, NodeDeclaration node)
    {
        var status = WorkPerformer.StatusOf(record, node.Id);
        return new RunReviewStep(node.Id, node.Kind, status.State) { Code = status.Code, Words = status.Words, Since = status.Since };
    }
}
