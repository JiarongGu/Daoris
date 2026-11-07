namespace Daoris.Driver;

/// <summary>
/// What the branch form made, and what a plugin is told of it: where, from which line, which commits — and the session
/// branch it was made from. At file level, as the squash's advance (<see cref="TreeSquashes"/>) makes one too.
/// </summary>
internal sealed record Branched(
    TreeLanding Landing, string Root = "", string? Base = null, IReadOnlyList<LandingCommit>? Carried = null, string? Source = null)
{
    public IReadOnlyList<LandingCommit> Commits => Carried ?? [];

    /// <summary>The session's tip squashed into the branch's new commit, which the record keeps; null where nothing was squashed.</summary>
    public string? Squashed { get; init; }

    /// <summary>What the landing replayed onto the line, leaving out what it holds; null where it landed the commits as made.</summary>
    internal LandReplay? Replay { get; init; }

    /// <summary>The tree's own branch was replayed in place: the tree already stands on what landed.</summary>
    public bool Followed { get; init; }

    public static implicit operator Branched(TreeLanding refused) => new(refused);
}

