namespace Daoris.Driver;

/// <param name="Path">Where the tree is — what the session record carries and the lock keys on.</param>
/// <param name="Branch">The session's own branch, never reused; git itself refuses a second use.</param>
/// <param name="BasedOn">Which point the branch grew from, spelled for a person — the canonical line, or HEAD with the reason.</param>
/// <param name="Sentence">The whole act in one sentence, price included — for whoever is watching.</param>
/// <param name="GrewFrom">The chain step's branch it grew from, when it did — null for the canonical line.</param>
public sealed record TreeOpened(string Path, string Branch, string BasedOn, string Sentence, string? GrewFrom = null)
{
    /// <summary>
    /// <see cref="Sentence"/> with its codes: the tree's branch and what it grew
    /// from as facts, never its path; and where its start went unrecorded, that line with git's words beside it.
    /// </summary>
    public Noted? Opening { get; init; }
}

