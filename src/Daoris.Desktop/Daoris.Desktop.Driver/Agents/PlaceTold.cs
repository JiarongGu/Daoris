namespace Daoris.Driver;

/// <summary>What a turn in place is told at its start: the paths changed outside every session since the place's last manifest, the
/// uncommitted paths that are not its line's own, and what its line's last turn changed by none of its tools (<see cref="TurnUnattributed"/>).</summary>
/// <param name="Outside">Added, changed or removed by the person or another program since the last session here, sorted.</param>
/// <param name="Others">Uncommitted at the start and not the line's own: the person's, or those of a line the person let go.</param>
public sealed record PlaceTold(IReadOnlyList<string> Outside, IReadOnlyList<string> Others)
{
    /// <summary>The place's kind (<see cref="PlaceKind"/>), and whether a folder is past the manifest's bounds.</summary>
    public string Kind { get; init; } = PlaceKind.Git;

    public bool TooLarge { get; init; }

    public IReadOnlyList<string> Unattributed { get; init; } = [];

    public bool Equals(PlaceTold? other) =>
        other is not null && Outside.SequenceEqual(other.Outside, StringComparer.Ordinal) && Others.SequenceEqual(other.Others, StringComparer.Ordinal)
        && Kind == other.Kind && TooLarge == other.TooLarge && Unattributed.SequenceEqual(other.Unattributed, StringComparer.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Outside.Count, Others.Count, Kind, TooLarge);
}
