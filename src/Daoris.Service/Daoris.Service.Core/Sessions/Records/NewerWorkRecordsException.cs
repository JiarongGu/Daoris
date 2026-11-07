namespace Daoris.Knowledge;

/// <summary>
/// The records are stamped newer than this build reads: no row is read or
/// written, and a post is refused, so the driver keeps its folder <c>deleted</c>, awaiting its record, and says why.
/// </summary>
public sealed class NewerWorkRecordsException(int found, int known)
    : Exception($"The records of finished trees were written by a newer Daoris (version {found}; this build reads {known}), so this one keeps none of them.")
{
    /// <summary>The stamp the store carries.</summary>
    public int Found { get; } = found;

    /// <summary>The version this build writes.</summary>
    public int Known { get; } = known;
}
