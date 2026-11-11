namespace Daoris.Driver;

/// <summary>
/// The review record a step writing the work is fed, by reference, never the work as it stands when the step runs: where the graph holds a
/// review, the record of the attempt the last review on every way to the step accepted (<see cref="EffectOrder.Fed"/>); where it holds none,
/// the run's work read into a record at the step's start and kept under the step and its attempt, so every start of that attempt writes the
/// same thing.
/// </summary>
internal static class EffectRecord
{
    /// <summary>The record <paramref name="kept"/>'s step writes; null where its review's record does not read.</summary>
    public static ReviewRecord? Of(NodeKept kept)
    {
        var (graph, run, node, attempt) = (kept.Governed.Graph, kept.Scope.Run.Binding.Run, kept.Node.Id, kept.Status.Attempt);
        if (graph.Nodes.Any(each => each.Kind == NodeKinds.Review))
        {
            return EffectOrder.Fed(graph, node) is { } review && WorkPerformer.StatusOf(kept.Record, review) is { State: NodeStates.Reached, Acceptance: { } accepted }
                ? ReviewRecords.Read(kept.Home, run, review, accepted.Attempt)
                : null;
        }

        if (ReviewRecords.Read(kept.Home, run, node, attempt) is { } read) return read;
        var record = ReviewWork.Read(kept.Home, kept.Scope, null).Compose(kept.Home, run, node, attempt, kept.At);
        ReviewRecords.Keep(kept.Home, record);
        return ReviewRecords.Read(kept.Home, run, node, attempt) ?? record;
    }
}
