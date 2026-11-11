namespace Daoris.Driver;

/// <summary>
/// The runs an observed event told under a branch is for. A branch carries one run's work, and its event is that run's. A branch two graph runs
/// share is each one's subject, and is told once for both: its event goes to each run whose own node of
/// the event's step is begun on that branch at the event's commit, and where none is, to each run whose graph names that step, never to
/// whichever run the branch maps to first.
/// </summary>
internal static class ToldRuns
{
    /// <summary>The runs <paramref name="mapped"/>'s told branch is for, by the event's step kind and commit; <paramref name="mapped"/> alone where it is no run's or no other run shares it.</summary>
    public static IReadOnlyList<GovernedUnit> Of(
        GovernedUnit mapped, IReadOnlyDictionary<string, GovernedUnit> governed, string told, string kind, string? commit, Func<string, NodeRecord?> read)
    {
        if (mapped.Scope is null) return [mapped];
        var sharing = governed.Values.Where(each => each.Scope is { } scope && scope.Told.Contains(told, StringComparer.Ordinal))
            .Distinct().OrderBy(each => each.Unit.Id, StringComparer.Ordinal).ToList();
        if (sharing.Count < 2) return [mapped];

        var naming = sharing.Where(each => Named(each, kind) is not null).ToList();
        var awaiting = naming.Where(each => read(each.Unit.Id)?.Statuses.GetValueOrDefault(Named(each, kind)!.Id) is
            { State: NodeStates.Active, Binding: { } binding }
            && binding.Observes == told && string.Equals(binding.SubjectRevision, commit, StringComparison.OrdinalIgnoreCase)).ToList();
        return awaiting.Count > 0 ? awaiting : naming.Count > 0 ? naming : [mapped];
    }

    /// <summary>The one observed node of <paramref name="kind"/> a run's graph names and observes; null where it names none, or two.</summary>
    public static NodeDeclaration? Named(GovernedUnit run, string kind)
    {
        var named = run.Graph.Nodes.Where(node => node.Kind == kind && node.Reach == NodeReach.Observed).ToList();
        return named.Count == 1 && run.Observable.Contains(named[0].Id) ? run.Declared[named[0].Id] : null;
    }
}
