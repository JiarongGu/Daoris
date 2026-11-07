namespace Daoris.Driver;

/// <summary>
/// What the two holds, <c>quest.holds</c> and <c>content.holds</c>, share (LANDNODE1k; D145's and D173's notes of 2026-11-01): the holds a landing's
/// start judged outside the graph, as steps of it, each observed by Daoris at the bound commit and begun as every observed built-in is. Pure: the
/// caller hands in what git read, and nothing is judged again or written.
/// </summary>
/// <remarks>
/// <para><b>A hold keeps the step active, never failed.</b> Each can let go later (a yes, a verdict, a new commit), so nothing fails it; the person's
/// pass with words is the way past one, as with <c>work.requests</c>.</para>
/// <para><b>Under Current a door judges them where it lands</b>, so a landing its record keeps reaches both, as a landed opinion's gate does.</para>
/// </remarks>
internal static class HoldReading
{
    /// <summary>The first of <paramref name="names"/> written on <paramref name="node"/> as anything but <c>true</c> or <c>false</c>, by its name; null where each holds.</summary>
    public static string? Problem(NodeDeclaration node, IReadOnlyList<string> names) =>
        names.FirstOrDefault(name => node.Options.TryGetValue(name, out var raw) && NodeOptions.Value(raw) is not bool);

    /// <summary>Whether a hold's option is on: true where it is not written.</summary>
    public static bool On(NodeDeclaration node, string option) => !node.Options.TryGetValue(option, out var raw) || NodeOptions.Value(raw) is not false;

    /// <summary>
    /// What a hold's reading settles before its own: another subject, node or version is no observation, and a landing recorded at the bound
    /// commit lets the hold go, since what its door judged is kept. Null where the kind's own reading goes on.
    /// </summary>
    public static NodeObservation? Settled(NodeReading reading)
    {
        if (NodeObserving.NotBound(reading) is { } refused) return refused;
        var (unit, binding) = (reading.Unit, reading.Binding);
        return unit.Kind == WorkUnitKind.Branch && LandedAt(unit, binding) is { } landed
            ? Reached(reading, LandedBranches.FileName, binding.SubjectRevision ?? landed.Tip, $"`{landed.Branch}` landed: what its door judged let it go")
            : null;
    }

    /// <summary>The hold's reach by <see cref="NodeSource.Holds"/>, its evidence the record's entry for the unit.</summary>
    public static NodeObservation Reached(NodeReading reading, string record, string? revision, string said) =>
        Seen(reading, NodeStates.Reached, said) with
        {
            Source = NodeSource.Holds,
            Acceptance = reading.Binding with
            {
                Evidence = new NodePort(NodeObserving.EvidenceSchema(reading.Node.Kind), $"{record}#{reading.Unit.Id}", revision, reading.Binding.Id),
            },
        };

    /// <summary>What the hold is observed as: <paramref name="outcome"/>, or none yet, with what its records say, which a hold with none keeps as its reason.</summary>
    public static NodeObservation Seen(NodeReading reading, string? outcome, string said) =>
        NodeObserving.Seen(reading, outcome, said) with { Holds = outcome is null ? said : null };

    /// <summary>
    /// What the save refuses of a hold in <paramref name="graph"/>: each hold once, in a graph that lands, and before every landing by the
    /// <c>work.requests</c> reading (<see cref="NodeGraphStarts.StartsWithout"/>), so a hold after the landing or beside it in an <c>anyOf</c> is
    /// refused alike. Each is said once, against the hold.
    /// </summary>
    public static IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration hold)
    {
        var standing = GraphStanding.Of(graph);
        var first = standing.ById.Values.First(node => node.Kind == hold.Kind);
        if (first.Id != hold.Id)
        {
            yield return new WireProblem(GraphRefusal.HoldTwice, null, hold.Id, $"node `{hold.Id}`: `{first.Id}` and `{hold.Id}` are both a `{hold.Kind}`, and the graph judges each hold once.");
        }

        if (standing.Landings.Count == 0)
        {
            yield return new WireProblem(GraphRefusal.HoldUnlanded, null, hold.Id, $"node `{hold.Id}`: a `{hold.Kind}` judges the work before it lands, and the graph holds no landing.");
            yield break;
        }

        var early = NodeGraphStarts.StartsWithout(standing.ById, standing.Starts, standing.Local, node => node.Id == hold.Id);
        foreach (var landing in standing.Landings.Where(landing => early.Contains(landing.Id)))
        {
            yield return new WireProblem(GraphRefusal.HoldLate, null, hold.Id, $"node `{hold.Id}`: the landing `{landing.Id}` can start before `{hold.Id}` is reached, and a `{hold.Kind}` judges the work before it lands: "
                + $"every way to it passes `{hold.Id}`.");
        }
    }

    /// <summary>
    /// The landing a branch unit's records keep for <paramref name="binding"/>: on a run's binding, the one made at or squashing its bound commit,
    /// or the merge its session branch records there; on a unit's own, its newest, or the merge its session branch records. Null where none is kept.
    /// </summary>
    private static LandedBranch? LandedAt(WorkUnit unit, NodeAcceptance binding)
    {
        if (binding.Subject is not null)
        {
            if (binding.SubjectRevision is not { } bound) return null;
            // LANDNODE1m: a merge keeps no landing record, and a replayed one leaves its tree on the commit merged, which the run then binds.
            return unit.Records.Landings.LastOrDefault(each => each.Tip == bound || each.Squashed == bound)
                ?? (unit.Records.Branch?.Held is { } held && string.Equals(held.Tip, bound, StringComparison.OrdinalIgnoreCase) ? Merged(unit) : null);
        }

        return NodeObserving.LandingOf(unit) ?? Merged(unit);
    }

    /// <summary>The merge a branch unit's session branch records, read as a landing at the commit the line holds; null where none is held.</summary>
    private static LandedBranch? Merged(WorkUnit unit) =>
        unit.Records.Branch is { Held: { } merged } branch
            ? new LandedBranch(unit.Owner.Repository ?? "", unit.Owner.Workspace, branch.Branch, null, merged.Tip, unit.Executors.LastOrDefault() ?? "", null, null, merged.At)
            : null;
}
