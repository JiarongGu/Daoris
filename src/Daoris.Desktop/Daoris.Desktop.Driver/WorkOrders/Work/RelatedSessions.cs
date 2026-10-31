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

/// <summary>One session beside another, what meets and how it was read.</summary>
/// <param name="Session">The other session.</param>
/// <param name="Kind">Its record's kind.</param>
/// <param name="Quest">Its quest, or null.</param>
/// <param name="Read"><c>read</c>, or <c>not-read</c> where git cannot say (D137).</param>
/// <param name="ReadAt">When the last good read of its tree was made: set for <c>not-read</c> after a good one, so the page says from when.</param>
/// <param name="DeclaredOverlap">The asking session's declared paths that meet the other's declared or changed ones.</param>
/// <param name="ChangedOverlap">The changed paths both hold.</param>
public sealed record RelatedSession(
    string Session, string Kind, string? Quest, string Read, DateTimeOffset? ReadAt,
    IReadOnlyList<string> DeclaredOverlap, IReadOnlyList<string> ChangedOverlap);

/// <summary>
/// The sessions of one repository beside one another (LINESTOGETHER1c, the review-flow design §3): each session with a tree here,
/// chats included, compared for declared paths that meet and changed paths both hold. A tree's read is kept by its base and commit
/// only while the session has ended and the tree holds no uncommitted path; any other is read at each look. A read failing after
/// a good one answers <c>not-read</c>, never the old paths.
/// </summary>
public sealed class RelatedSessions(RelatedSessions.Reads reads, Func<DateTimeOffset> now)
{
    /// <summary>What git says of a tree: its commit, whether it holds an uncommitted path, and the paths changed since a base.</summary>
    public sealed record Reads(
        Func<string, CancellationToken, Task<string?>> Head,
        Func<string, CancellationToken, Task<bool?>> Dirty,
        WorkOverlaps.Diff Diff);

    private sealed record Kept(string Key, IReadOnlyList<string> Paths, DateTimeOffset At);

    private readonly Dictionary<string, Kept> _good = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Kept> _clean = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The real world: git's reads in the tree, read-only.</summary>
    public static Reads GitReads { get; } = new(
        async (tree, ct) =>
        {
            var (code, stdout, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "rev-parse", "HEAD"], ct).ConfigureAwait(false);
            return code == 0 ? stdout.Trim() : null;
        },
        async (tree, ct) =>
        {
            var (code, stdout, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "status", "--porcelain"], ct).ConfigureAwait(false);
            return code == 0 ? stdout.Trim().Length > 0 : null;
        },
        WorkOverlaps.DiffAsync);

    /// <summary>What each of <paramref name="others"/> meets of <paramref name="mine"/>, the ones whose work meets none left out.</summary>
    public async Task<IReadOnlyList<RelatedSession>> LookAsync(
        RelatedCandidate mine, IReadOnlyList<RelatedCandidate> others, CancellationToken ct = default)
    {
        var own = await ReadAsync(mine, ct).ConfigureAwait(false);
        var found = new List<RelatedSession>();
        foreach (var other in others.Where(other => !string.Equals(other.Id, mine.Id, StringComparison.OrdinalIgnoreCase)))
        {
            var theirs = await ReadAsync(other, ct).ConfigureAwait(false);
            if (own.Paths is null || theirs.Paths is null)
            {
                // Where the asker's own tree cannot be read, the others still say what is theirs.
                found.Add(new(other.Id, other.Kind, other.Quest, "not-read", (theirs.At ?? own.At), [], []));
                continue;
            }

            var declared = mine.Declared.Count == 0 ? [] : PathOverlap.Meet(mine.Declared, other.Declared, theirs.Paths);
            var changed = PathOverlap.Shared(own.Paths, theirs.Paths);
            if (declared.Count > 0 || changed.Count > 0) found.Add(new(other.Id, other.Kind, other.Quest, "read", null, declared, changed));
        }

        return found;
    }

    private async Task<(IReadOnlyList<string>? Paths, DateTimeOffset? At)> ReadAsync(RelatedCandidate session, CancellationToken ct)
    {
        var head = await reads.Head(session.Tree, ct).ConfigureAwait(false);
        var key = $"{session.BaseCommit}:{head}";
        var dirty = head is null ? null : await reads.Dirty(session.Tree, ct).ConfigureAwait(false);
        if (!session.Running && dirty == false && _clean.TryGetValue(session.Tree, out var kept) && kept.Key == key)
            return (kept.Paths, kept.At);

        var paths = dirty is null ? null : await reads.Diff(session.Tree, session.BaseCommit, ct).ConfigureAwait(false);
        if (paths is null)
        {
            _clean.Remove(session.Tree);
            return (null, _good.TryGetValue(session.Tree, out var earlier) ? earlier.At : null);
        }

        var good = new Kept(key, paths, now());
        _good[session.Tree] = good;
        if (!session.Running && dirty == false) _clean[session.Tree] = good;
        else _clean.Remove(session.Tree);
        return (paths, good.At);
    }
}
