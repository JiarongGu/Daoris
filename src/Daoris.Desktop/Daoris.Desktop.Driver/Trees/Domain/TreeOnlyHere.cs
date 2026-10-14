namespace Daoris.Driver;

/// <summary>
/// What abandon's proof found of one session tree and its branch (PAUSE1d, D132 point 7, design §3.3): a word from a fixed
/// list, in the public spelling the page and the terminal share.
/// </summary>
public static class OnlyHereKind
{
    /// <summary>No commit after its base is on any ref but this machine's local <c>daoris/*</c> branches: discarded.</summary>
    public const string OnlyHere = "only-here";

    /// <summary>A commit after its base is on another ref (the line, a branch of the person's, a remote, a tag): kept whole.</summary>
    public const string Elsewhere = "elsewhere";

    /// <summary>Its branch is checked out somewhere other than its own tree: somebody works in it, so it is kept.</summary>
    public const string CheckedOut = "checked-out";

    /// <summary>git could not say: kept, as the clean-up keeps what it cannot judge.</summary>
    public const string Unknown = "unknown";

    /// <summary>Neither the tree's folder nor its branch is on this machine any more: nothing to discard.</summary>
    public const string Gone = "gone";
}

/// <summary>Abandon's proof of one tree and its branch, and what the first press names of them (design §3.2, §3.3).</summary>
/// <param name="Kind">One of <see cref="OnlyHereKind"/>.</param>
public sealed record TreeOnlyHere(string Kind)
{
    /// <summary>The repository's checkout git answered for: this machine's path, never handed to a page or a record.</summary>
    public string? Root { get; init; }

    /// <summary>Whether the tree's folder is here, a repository of its own; false is its branch alone.</summary>
    public bool TreeHere { get; init; }

    /// <summary>The branch's tip, which brings a deleted branch back while git keeps its commits.</summary>
    public string? Tip { get; init; }

    /// <summary>How many commits after the base it holds, up to the tip; null where git could not count.</summary>
    public int? Commits { get; init; }

    /// <summary>How many paths in the tree carry uncommitted changes; null where there is no tree to ask.</summary>
    public int? Uncommitted { get; init; }

    /// <summary>Up to <see cref="OnlyHereProof.FilesNamed"/> of those paths, relative to the repository.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>For <see cref="OnlyHereKind.Elsewhere"/>: a ref that holds one of its commits, as git names it short.</summary>
    public string? Where { get; init; }

    /// <summary>Whether the abandon may discard it: nothing it holds is anywhere else.</summary>
    public bool Discards => Kind == OnlyHereKind.OnlyHere;
}
