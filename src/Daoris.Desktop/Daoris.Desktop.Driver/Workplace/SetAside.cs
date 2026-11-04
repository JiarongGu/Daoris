using System.Globalization;
using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeWorktrees;

namespace Daoris.Driver;

/// <summary>A folder the look moved aside, met by a later look's purge (AUTOTIDY1): deleted, or kept and why.</summary>
/// <param name="Moved">When it was moved aside, read from its name.</param>
/// <param name="Kept">Kept: why, in a clause.</param>
/// <param name="Code">Kept: why, as the machine log's code (<see cref="SetAsideKept"/>).</param>
internal sealed record SetAside(string Folder, DateTimeOffset Moved, bool Purged, string? Kept, string? Code = null)
{
    /// <summary>Purged: the bytes its files held (KEEP1a, <c>keeping.json</c>'s <c>last</c>).</summary>
    public long Bytes { get; init; }

    /// <summary>Its class when the purge met it (KEEPSIZE1b, <see cref="SetAsideClass"/>).</summary>
    public string Class { get; init; } = SetAsideClass.Unmarked;

    /// <summary>It was met by the hour's rule, an hour after its record, not its days.</summary>
    public bool Hour { get; init; }
}
