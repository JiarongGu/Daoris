namespace Daoris.Driver;

/// <summary>A session of one repository with a tree here, as the comparison takes it.</summary>
/// <param name="Id">The session's id.</param>
/// <param name="Kind">Its record's kind, <c>driven</c> or <c>chat</c>.</param>
/// <param name="Tree">The tree it holds.</param>
/// <param name="BaseCommit">The commit its tree stood at when it opened.</param>
/// <param name="Running">Whether it runs now: a running tree is read at each look.</param>
/// <param name="Declared">The paths its quest declared, none for a chat.</param>
/// <param name="Quest">The quest it serves, or null.</param>
public sealed record RelatedCandidate(
    string Id, string Kind, string Tree, string BaseCommit, bool Running, IReadOnlyList<string> Declared, string? Quest = null);

/// <summary>Which sessions a look compares.</summary>
public static class RelatedCandidates
{
    /// <summary>
    /// The asking session and the other sessions of its repository with a tree and a base, only where the tree folder exists: a gone
    /// tree has no work to meet and no git is run on it. Null where the asker is unknown, holds no tree, or its tree is gone.
    /// </summary>
    public static (RelatedCandidate Mine, IReadOnlyList<RelatedCandidate> Others)? Pick(
        IReadOnlyList<SessionView> all, IReadOnlySet<string> running, Func<string, IReadOnlyList<string>> declaredOf,
        string id, Func<string, bool> treeExists)
    {
        var asking = all.FirstOrDefault(session => string.Equals(session.Id, id, StringComparison.OrdinalIgnoreCase));
        if (asking is null) return null;

        RelatedCandidate? Candidate(SessionView session) => session is { Tree.Length: > 0, BaseCommit.Length: > 0 } && treeExists(session.Tree)
            ? new(session.Id, session.Kind, session.Tree, session.BaseCommit, running.Contains(session.Id),
                session.Quest is { Length: > 0 } quest ? declaredOf(quest) : [], session.Quest)
            : null;

        if (Candidate(asking) is not { } mine) return null;
        var others = all.Where(session => !session.Id.Contains('/') && string.Equals(session.Repository, asking.Repository, StringComparison.OrdinalIgnoreCase))
            .Select(Candidate).OfType<RelatedCandidate>().ToList();
        return (mine, others);
    }
}
