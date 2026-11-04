namespace Daoris.Driver;

/// <param name="Path">Where the tree is — what the session record carries and the lock keys on.</param>
/// <param name="Branch">The session's own branch, never reused; git itself refuses a second use.</param>
/// <param name="BasedOn">Which point the branch grew from, spelled for a person — the canonical line, or HEAD with the reason.</param>
/// <param name="Sentence">The whole act in one sentence, price included — for whoever is watching.</param>
/// <param name="GrewFrom">The chain step's branch it grew from (CHAIN2), when it did — null for the canonical line.</param>
public sealed record TreeOpened(string Path, string Branch, string BasedOn, string Sentence, string? GrewFrom = null)
{
    /// <summary>
    /// <see cref="Sentence"/> with its codes (LANG1a, the language design §4 rows 25–26): the tree's branch and what it grew
    /// from as facts, never its path; and where its start went unrecorded, that line with git's words beside it.
    /// </summary>
    public Noted? Opening { get; init; }

    /// <summary>
    /// What its session's agent is told of what was copied into it from its checkout: the dependencies (TREEDEPS1), and to skip the
    /// <c>npm ci</c> that would delete them; then the machine-local files its gates read (TREELOCAL1, D216), and to leave them as they
    /// are. Null where nothing was. Its name is the dependencies', which its readers carry as one told-once field.
    /// </summary>
    public string? Dependencies { get; init; }
}

