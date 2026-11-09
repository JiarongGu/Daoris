namespace Daoris.Driver;

/// <summary>
/// A <c>resolve</c> read from what its door wrote (<see cref="ResolveRecords"/>, handed to the pass as <see cref="NodeGitReads.Resolved"/>):
/// <c>resolved</c> once its resolution finished the rebase; failed where a stop's bound is spent, its resolver stood down or the resolution
/// changed a file the problem did not name; else active, its wait said.
/// </summary>
internal static class ResolveReading
{
    public static NodeObservation Observe(NodeReading reading)
    {
        if (NodeObserving.NotBound(reading) is { } refused) return refused;
        var binding = reading.Binding;
        var run = RunUnits.Is(binding.Unit) ? binding.Unit[RunUnits.Prefix.Length..] : binding.Unit;
        var (made, waits) = reading.Git?.Resolved?.Invoke(run, reading.Node.Id, binding.Attempt) ?? (null, null);
        if (made is null) return NodeObserving.Seen(reading, null, waits ?? "Hands its problem to its resolver.");
        if (made.Outcome == NodeStates.Failed) return NodeObserving.Seen(reading, NodeStates.Failed, made.Said) with { Source = NodeSource.Git, Code = NodeSource.Git };
        var reached = NodeObserving.Reached(reading, NodeSource.Git, binding.SubjectRevision, "resolves", made.Commit ?? "", made.Said);
        return reached with { Acceptance = reached.Acceptance! with { Outcome = ResolveNode.Resolved } };
    }

    /// <summary>The pass's reads with what the run's <c>resolve</c> doors wrote: each attempt's end and the node's wait.</summary>
    public static NodeGitReads Reads(string home, NodeGitReads reads)
    {
        var records = new ResolveRecords(home);
        var read = new Dictionary<string, ResolveRun>(StringComparer.Ordinal);
        return reads with
        {
            Resolved = (run, node, attempt) =>
            {
                if (!read.TryGetValue(run, out var all)) read[run] = all = records.Of(run);
                return (all.Attempts.LastOrDefault(each => each.Node == node && each.Attempt == attempt),
                    all.Stops.LastOrDefault(each => each.Node == node && each.Waits is not null)?.Waits);
            },
        };
    }
}
