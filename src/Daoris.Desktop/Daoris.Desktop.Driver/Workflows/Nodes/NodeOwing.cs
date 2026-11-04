namespace Daoris.Driver;

/// <summary>
/// Which governed units a pass judges: those owing a start or a rebinding, the turn they are taken in when more than a pass holds, and the
/// records Current's graphs left on a run's quests and branches.
/// </summary>
internal sealed class NodeOwing(NodeRecordFiles records, Func<IIntegrationDoor?> integration)
{
    /// <summary>The last unit a pass evaluated when the units concerned passed <see cref="NodeRuntime.UnitsPerPass"/>: the next pass begins after it.</summary>
    private string? _after;

    /// <summary>Forgets where the rotation stood, once the runtime disarms.</summary>
    public void Forget() => _after = null;

    /// <summary>
    /// The units a pass evaluates, in id order: all of them up to <see cref="NodeRuntime.UnitsPerPass"/>; past it, the next
    /// <see cref="NodeRuntime.UnitsPerPass"/> after the last unit the pass before reached, round again from the first, so no unit waits behind
    /// the same prefix at every pass. A unit whose event the hearing held (<paramref name="held"/>) is judged first, outside the rotation, so
    /// its node rebinds in the pass that heard the event; the rotation fills what is left of the turn, and moves on only by what it took.
    /// </summary>
    public List<GovernedUnit> Turn(IEnumerable<GovernedUnit> concerned, IReadOnlySet<string> held)
    {
        const int perPass = NodeRuntime.UnitsPerPass;
        var ordered = concerned.OrderBy(each => each.Unit.Id, StringComparer.Ordinal).ToList();
        var first = ordered.Where(each => held.Contains(each.Unit.Id)).Take(perPass).ToList();
        var rest = ordered.Where(each => !held.Contains(each.Unit.Id)).ToList();
        if (ordered.Count <= perPass) return [.. first, .. rest];
        var next = _after is null ? 0 : rest.FindIndex(each => string.CompareOrdinal(each.Unit.Id, _after) > 0);
        if (next < 0) next = 0;
        var rotation = rest.Skip(next).Concat(rest.Take(next)).Take(perPass - first.Count).ToList();
        if (rotation.Count > 0) _after = rotation[^1].Unit.Id;
        return [.. first, .. rotation];
    }

    /// <summary>
    /// Whether a unit's record holds one of its dispatchable nodes ready: a start owed, judged at a wake as well as at the look. A run's too,
    /// now its passes start its performed nodes.
    /// </summary>
    public bool OwesStart(GovernedUnit governed) =>
        records.Read(governed.Unit.Id) is { } record
        && governed.Dispatchable.Concat(governed.Observable).Concat(IntegrateIds(governed)).Any(node => record.Statuses.GetValueOrDefault(node)?.State == NodeStates.Ready);

    /// <summary>
    /// The unit's <c>integrate</c> nodes: waited, so neither dispatchable nor observable, yet a start owed when held ready (for the card, or a
    /// gate its request's outcome lifted), so a wake judges the unit and the look's deadline is not the only thing that does. Only where a door
    /// is set: a runtime with none starts none, and owes none.
    /// </summary>
    private IEnumerable<string> IntegrateIds(GovernedUnit governed) =>
        integration() is null ? [] : governed.Declared.Values.Where(node => node.Kind == NodeKinds.Integrate).Select(node => node.Id);

    /// <summary>
    /// Whether a branch unit's observed node awaits its event on a revision its branch's tip, read this pass, has moved off: judged at this
    /// pass, a wake as well as a look, so it rebinds before the event at the new tip comes. The checkout is read only where such a node is
    /// active; an unread tip rebinds nothing.
    /// </summary>
    public async Task<bool> RebindsAsync(GovernedUnit governed, PassTips tips, CancellationToken ct)
    {
        // A run's observed node before `handed` binds its branch's tip, and rebinds when that branch moves, as a branch unit's.
        if (governed.Scope is { Branch: { } branch } scope)
        {
            if (records.Read(governed.Unit.Id) is not { } held) return false;
            var bound = governed.Observable.Where(node => !scope.AfterHanded.Contains(node))
                .Select(node => held.Statuses.GetValueOrDefault(node))
                .Where(status => status is { State: NodeStates.Active, Binding: { } binding } && binding.Observes == branch.Id)
                .Select(status => status!.Binding!.SubjectRevision).OfType<string>().ToList();
            if (bound.Count == 0) return false;
            var moved = await tips.TipAsync(scope.Root, branch.Subject.Artifact ?? "", ct).ConfigureAwait(false);
            return moved is not null && bound.Any(revision => !string.Equals(revision, moved, StringComparison.Ordinal));
        }

        if (!NodeEvaluations.Tipped(governed) || records.Read(governed.Unit.Id) is not { } record) return false;
        var awaiting = governed.Observable.Select(node => record.Statuses.GetValueOrDefault(node)?.Binding is { } binding
                && record.Statuses[node].State == NodeStates.Active ? binding.SubjectRevision : null)
            .OfType<string>().ToList();
        if (awaiting.Count == 0) return false;
        var tip = await tips.TipAsync(governed.Watched.Root, governed.Watched.Branch, ct).ConfigureAwait(false);
        return tip is not null && awaiting.Any(revision => !string.Equals(revision, tip, StringComparison.Ordinal));
    }

    /// <summary>
    /// What a fresh read says of the records Current's graphs wrote on a run's quests and branches before the run was bound to a graph version
    /// (the cutover design §2.3): each left as it stands, no graph governing it now, so an attempt on it not settled is held for the person as
    /// a removed graph's is; one line each, once a look.
    /// </summary>
    public IEnumerable<string> LeftBefore(IEnumerable<GovernedUnit> governed)
    {
        foreach (var each in governed.Where(each => each.Scope is not null))
        {
            var units = each.Scope!.Quests.Select(quest => QuestUnits.IdOf(quest.Id)).Concat(each.Scope.Branches.Select(branch => branch.Id));
            foreach (var unit in units.Distinct(StringComparer.Ordinal))
            {
                if (!File.Exists(NodeRecordFiles.PathOf(records.Home, unit))) continue;
                if (records.Read(unit) is { } left && left.Attempts.All(attempt => attempt.State == AttemptStates.Settled)) continue;
                yield return $"work  {unit}: a record Current's graphs wrote on it before its run `{each.Run}` was bound to a graph version is left as "
                             + "it stands; nothing governs it now, so what it holds unsettled waits for you.";
            }
        }
    }
}
