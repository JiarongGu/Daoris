namespace Daoris.Driver;

/// <summary>
/// A <c>land</c> attempt left unreconciled by its binding's lapse (LANDNODE1m), settled by the deliveries its doors land through. A landing
/// that replayed the work onto a moved line leaves its tree on the commit it merged, so the run's next pass binds that commit and lapses the
/// attempt that landed; and a commit made while <c>land</c> was active lapses it with nothing landed. Its effect is a delivery at the commit it
/// was bound to (the land node design §3): where none stands unresolved there, what it did is known, delivered, withdrawn or stopped, with git
/// holding what it made, so it holds no new attempt back. One unresolved there still does, until the settlement by phase reads it.
/// </summary>
public static class LandAttempts
{
    /// <summary>Whether <paramref name="record"/> holds a <c>land</c> attempt of <paramref name="graph"/>'s that a lapse left unreconciled.</summary>
    public static bool Holds(NodeGraph graph, NodeRecord record) => Stale(graph, record).Any();

    /// <summary>
    /// <paramref name="record"/> with each such attempt settled where no delivery bound at its commit stands unresolved; null where none is.
    /// </summary>
    /// <param name="unresolved">Whether a delivery of the unit's repository bound at the commit stands unresolved.</param>
    public static NodeRecord? Settled(NodeGraph graph, NodeRecord record, Func<string, bool> unresolved)
    {
        var next = record;
        foreach (var (status, attempt, tip) in Stale(graph, record))
        {
            if (unresolved(tip)) continue;
            next = next with { Statuses = WorkPerformer.With(next.Statuses, NodeLifecycle.Reconciled(status, attempt)) };
        }

        return ReferenceEquals(next, record) ? null : next;
    }

    private static IEnumerable<(NodeStatus Status, int Attempt, string Tip)> Stale(NodeGraph graph, NodeRecord record) =>
        graph.Nodes.Where(node => node.Kind == NodeKinds.Land)
            .Select(node => record.Statuses.GetValueOrDefault(node.Id))
            .Where(status => status is { Unreconciled: not null, State: not (NodeStates.Active or NodeStates.Unknown), Binding.SubjectRevision: not null })
            .Select(status => (status!, status!.Unreconciled!.Value, status.Binding!.SubjectRevision!));
}
