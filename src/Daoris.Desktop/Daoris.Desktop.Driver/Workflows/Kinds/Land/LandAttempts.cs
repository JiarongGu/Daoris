namespace Daoris.Driver;

/// <summary>
/// A <c>land</c> attempt left unreconciled by its binding's lapse (LANDNODE1m: a replayed merge's tree moved to the commit merged, or a commit
/// made while it was active), settled by the deliveries its doors land through: where none bound at its commit still holds it, what it did is
/// known, so it holds no new attempt back (<see cref="LandAttemptSettling"/> reads each by phase). Its commit is its binding's, or the one its
/// history kept once its judgement on a fresh binding took that away, so an attempt held once is settled when its delivery is.
/// </summary>
public static class LandAttempts
{
    /// <summary>Whether <paramref name="record"/> holds a <c>land</c> attempt of <paramref name="graph"/>'s that a lapse left unreconciled.</summary>
    public static bool Holds(NodeGraph graph, NodeRecord record) => Stale(graph, record).Any();

    /// <summary>
    /// <paramref name="record"/> with each such attempt settled where no delivery bound at its commit still holds it; null where none is.
    /// </summary>
    /// <param name="holding">Whether a delivery of the unit's repository bound at the commit still holds the attempt.</param>
    public static NodeRecord? Settled(NodeGraph graph, NodeRecord record, Func<string, bool> holding)
    {
        var next = record;
        foreach (var (status, attempt, tip) in Stale(graph, record))
        {
            if (holding(tip)) continue;
            next = next with { Statuses = WorkPerformer.With(next.Statuses, NodeLifecycle.Reconciled(status, attempt)) };
        }

        return ReferenceEquals(next, record) ? null : next;
    }

    /// <summary>The commits <paramref name="record"/>'s unreconciled <c>land</c> attempts were bound at, the deliveries there settling them.</summary>
    public static IReadOnlyList<string> Tips(NodeGraph graph, NodeRecord record) => [.. Stale(graph, record).Select(each => each.Tip).Distinct(StringComparer.OrdinalIgnoreCase)];

    private static IEnumerable<(NodeStatus Status, int Attempt, string Tip)> Stale(NodeGraph graph, NodeRecord record)
    {
        foreach (var node in graph.Nodes.Where(node => node.Kind == NodeKinds.Land))
        {
            if (record.Statuses.GetValueOrDefault(node.Id) is not { Unreconciled: { } attempt, State: not (NodeStates.Active or NodeStates.Unknown) } status) continue;
            if (TipOf(record, status, attempt) is { } tip) yield return (status, attempt, tip);
        }
    }

    /// <summary>
    /// The commit the attempt was bound at: its status's binding, or, once its judgement on a fresh binding took that away, the binding the
    /// history kept of it; null where neither holds it.
    /// </summary>
    private static string? TipOf(NodeRecord record, NodeStatus status, int attempt) =>
        status.Binding is { Attempt: var bound, SubjectRevision: { } tip } && bound == attempt ? tip
        : record.History.LastOrDefault(each => each.Was.Node == status.Node && each.Was.Binding is { SubjectRevision: not null } held && held.Attempt == attempt)
            ?.Was.Binding!.SubjectRevision;
}
