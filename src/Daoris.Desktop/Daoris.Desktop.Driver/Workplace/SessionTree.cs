namespace Daoris.Driver;

/// <param name="Path">Where it is.</param>
/// <param name="Workspace">Whose circle, read from the layout Daoris itself chose.</param>
/// <param name="Repository">Whose repository, same.</param>
/// <param name="Branch">
/// The session branch checked out in it; <see cref="NotATree"/> for a folder with no link to a repository, and
/// <see cref="SetAside"/> for a folder the look moved aside.
/// </param>
public sealed record SessionTree(string Path, string Workspace, string Repository, string Branch)
{
    /// <summary>What a folder with no <c>.git</c> is listed as (AUTOTIDY2c): no tree, which the look moves aside once it is left quiet.</summary>
    public const string NotATree = "(not a tree)";

    /// <summary>What a folder the look moved aside is listed as (AUTOTIDY1, AUTOTIDY2c), until the purge deletes it.</summary>
    public const string SetAside = "(moved aside)";

    /// <summary>When the look moved it aside, read from its name; null for anything not moved aside.</summary>
    public DateTimeOffset? MovedAside { get; init; }

    /// <summary>
    /// A folder moved aside past its date that the look's purge keeps (CLEANUP1, after review): why, in its own clause (a later write, a
    /// link, a folder that cannot be read). Null before its date, where the purge takes it, and for anything not moved aside.
    /// </summary>
    public string? PurgeKept { get; init; }

    /// <summary>
    /// A folder moved aside whose package folders the slim dropped (KEEP1a, the cleanup design §5): which, in git's spelling, from the
    /// manifest beside it. None for anything else.
    /// </summary>
    public IReadOnlyList<string> Slimmed { get; init; } = [];
}

