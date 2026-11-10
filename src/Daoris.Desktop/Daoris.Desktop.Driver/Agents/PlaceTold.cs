namespace Daoris.Driver;

/// <summary>What a turn in place is told at its start: the paths changed outside every session since the place's last manifest, and
/// the uncommitted paths that are not its line's own.</summary>
/// <param name="Outside">Added, changed or removed by the person or another program since the last session here, sorted.</param>
/// <param name="Others">Uncommitted at the start and not the line's own: the person's, or those of a line the person let go.</param>
public sealed record PlaceTold(IReadOnlyList<string> Outside, IReadOnlyList<string> Others)
{
    public bool Equals(PlaceTold? other) =>
        other is not null && Outside.SequenceEqual(other.Outside, StringComparer.Ordinal) && Others.SequenceEqual(other.Others, StringComparer.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Outside.Count, Others.Count);
}
