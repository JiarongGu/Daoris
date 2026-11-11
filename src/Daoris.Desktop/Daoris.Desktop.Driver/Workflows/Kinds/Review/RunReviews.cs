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
            Says = RunReviewSays.Of(read),
            Before = [.. Preceding(graph, review.Id).Select(node => Step(record, node, null))],
            Newer = read is not null && newest is not null && newest.Attempt > read.Attempt
                ? new RunReviewNewer(newest.Attempt, newest.Revision, Changed(read, newest)) : null,
            Follows = [.. Following(graph, review.Id).Select(each => Step(record, each.Node, each.Via))],
            Ended = record.Ended?.Outcome,
            Operations = read?.Kind == ReviewKinds.Plan ? PlanReview.Named(home, read).Plan?.Operations : null,
        };
    }

    /// <summary>What a reached step reached, by its evidence or its target: a pull request's address, a landed branch's ref.</summary>
    public static string? DoorOf(NodeStatus status) => status.Current is { } reached ? reached.Evidence?.Ref ?? reached.Target?.Ref : null;

    /// <summary>The paths whose content differs between two records: a row added, dropped or written to other content.</summary>
    public static IReadOnlyList<string> Changed(ReviewRecord from, ReviewRecord to)
    {
        var before = from.Rows.ToDictionary(row => row.Path, row => (row.Change, row.After), StringComparer.Ordinal);
        var after = to.Rows.ToDictionary(row => row.Path, row => (row.Change, row.After), StringComparer.Ordinal);
        return [.. before.Keys.Union(after.Keys, StringComparer.Ordinal)
            .Where(path => !before.TryGetValue(path, out var was) || !after.TryGetValue(path, out var now) || was != now)
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>The nodes an Accept may lead to, nearest first, each with the first wire that leads to it: a way back leads to none.</summary>
    private static List<(NodeDeclaration Node, Wire Via)> Following(NodeGraph graph, string review)
    {
        var found = new List<(string Id, Wire Via)>();
        var next = new Queue<string>([review]);
        while (next.TryDequeue(out var from))
        {
            foreach (var wire in graph.Wires.Where(wire => wire.From?.Node == from && wire.Kind is not (WireKinds.Back or WireKinds.Rejected)))
            {
                if (wire.To.Node is { } to && to != review && !found.Any(each => each.Id == to))
                {
                    found.Add((to, wire));
                    next.Enqueue(to);
                }
            }
        }

        return [.. found.Select(each => (graph.Nodes.FirstOrDefault(node => node.Id == each.Id), each.Via)).Where(each => each.Item1 is not null).Select(each => (each.Item1!, each.Via))];
    }

    /// <summary>The nodes the review comes after, in the graph's order: a way back to them is not a way before it.</summary>
    private static List<NodeDeclaration> Preceding(NodeGraph graph, string review)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var next = new Queue<string>([review]);
        while (next.TryDequeue(out var to))
        {
            foreach (var wire in graph.Wires.Where(wire => wire.To.Node == to && wire.Kind != WireKinds.Back && wire.From?.Node is { } from && from != review))
            {
                if (found.Add(wire.From!.Node!)) next.Enqueue(wire.From.Node!);
            }
        }

        return [.. graph.Nodes.Where(node => found.Contains(node.Id))];
    }

    private static RunReviewStep Step(NodeRecord record, NodeDeclaration node, Wire? via)
    {
        var status = WorkPerformer.StatusOf(record, node.Id);
        var newest = record.Newest(node.Id);
        IReadOnlyList<string> offers = newest is { Resolution: not null } ? []
            : status.State == NodeStates.Unknown ? newest is null ? HeldResolutions.Plugin : HeldResolutions.Of(newest)
            : newest is not null && WorkPerformer.KeptFailure(record, newest) ? HeldResolutions.Of(newest) : [];
        return new RunReviewStep(node.Id, node.Kind, status.State)
        {
            Attempt = status.Attempt, Code = status.Code, Words = status.Words, Since = status.Since, Via = via?.Kind, On = via?.On ?? via?.Outcome,
            Outcome = status.Current?.Outcome, Door = DoorOf(status), Offers = offers,
        };
    }
}
