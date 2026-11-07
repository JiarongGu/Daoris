namespace Daoris.Driver;

/// <summary>
/// A session's landed work: which files changed between the commit the tree stood at when the
/// spawn began and where it stands now, with the patch for each.
/// </summary>
/// <param name="Files">Every changed file, always — the LIST is never truncated, only the patches.</param>
/// <param name="Truncated">What the bound dropped, said out loud, or null when nothing is.</param>
/// <param name="Base">The commit the range is measured from.</param>
public sealed record TreeDiff(
    IReadOnlyList<DiffFile> Files, string? Truncated, string Base);

