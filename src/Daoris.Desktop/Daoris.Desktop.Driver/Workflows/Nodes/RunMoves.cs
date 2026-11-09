namespace Daoris.Driver;

/// <summary>A ref a run's own step moved: the unit that observes it, where it stood, where the step moved it, and the node and attempt that did.</summary>
/// <param name="Ref">The unit a binding observes the ref as: the branch unit of the work's branch.</param>
/// <param name="From">The revision it stood at.</param>
/// <param name="To">The revision the run's step moved it to.</param>
/// <param name="Node">The node that moved it.</param>
/// <param name="Attempt">That node's attempt.</param>
public sealed record RunMove(string Ref, string From, string To, string Node, int Attempt);

/// <summary>
/// A run's own move as its binding's revision: a step that moves a ref its run is bound to keeps the move on the run's record, and the pass
/// reads that ref at <c>from</c> while it stands at <c>to</c>, so nothing fed the work lapses because the run moved it. A move on from
/// <c>to</c> is new work.
/// </summary>
public static class RunMoves
{
    /// <summary>Keeps <paramref name="move"/> on the run's record <paramref name="unit"/>; false where the record could not be written.</summary>
    public static bool Keep(NodeRecordFiles records, string unit, RunMove move) =>
        records.Edit(unit, record => (record with { Moves = [.. record.Moves.Where(each => each != move), move] }, true)).Refused is null;

    /// <summary>
    /// The revision a binding reads <paramref name="reference"/> at, standing at <paramref name="tip"/>: the <c>from</c> of the newest move the
    /// run made to that tip, followed back through the run's earlier moves; the tip itself where the run moved it to no such place.
    /// </summary>
    public static string? Revision(IReadOnlyList<RunMove> moves, string reference, string? tip)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (tip is not null && seen.Add(tip)
               && moves.LastOrDefault(move => move.Ref == reference && string.Equals(move.To, tip, StringComparison.OrdinalIgnoreCase)) is { } moved)
        {
            tip = moved.From;
        }

        return tip;
    }
}
