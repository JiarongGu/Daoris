namespace Daoris.Driver;

/// <summary>
/// The person's doors for a kept session branch (LAND3, LAND4; PART10l-ii, out of <c>SessionTrees.Tidy.cs</c>): what the clean-up's
/// list offers beside a row, the session a row's tree is, and whether a removal names a path. Public and static, since the
/// terminal and the bridge say them too and the tidy (<see cref="TreeTidies"/>) is internal over the trees' context, as
/// <see cref="TreeFolders"/> was made public for the same reason in PART10k.
/// </summary>
public static class SessionBranchDoors
{
    /// <summary>
    /// The door offered beside a kept branch whose commits no branch of the person's holds (LAND3): a failed or superseded
    /// attempt's, which no landing's tidy and no clean-up takes, is discarded only by the person saying so. Null for every
    /// other row, and for one git could not judge.
    /// </summary>
    public static string? RemovalOffered(SweepItem item) => item.Kind == SweepKind.Unlanded && item.Commits > 0
        ? $"if its work is not wanted (a failed or superseded attempt): `daoris-driver trees remove {item.Branch} --repository {item.Repository} --force`"
        : null;

    /// <summary>
    /// The door offered beside a kept branch whose tree is still here and holds commits no branch of the person's holds (LAND4):
    /// its session's landing, the review's Accept from a terminal, whatever that session's ending. The landing checks
    /// everything at its press. Null for a branch whose tree is gone (its discard is the door), with no commits, that no
    /// record of this machine's names, or that a session still holds.
    /// </summary>
    /// <param name="session">The session the branch's tree is (<see cref="SessionOfTree"/>), or null.</param>
    public static string? LandingOffered(SweepItem item, SessionRecord? session) =>
        item is { Kind: SweepKind.Unlanded, Commits: > 0, Tree: not null } && session is { Live: false, Teammate: false }
            ? $"if its work is wanted: `daoris-driver trees land {session.Id}` accepts it (session {session.Id}, {session.State}, its tree here)"
            : null;

    /// <summary>
    /// The records a clean-up's list names each kept branch's session from (LAND4), or none where the service does not answer
    /// them: the landing line beside a row is the list's convenience, and the list stands without it.
    /// </summary>
    public static async Task<IReadOnlyList<SessionRecord>> RecordsOrNoneAsync(ServiceClient service, CancellationToken ct = default)
    {
        try
        {
            return await service.SessionRecordsAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// The session a tree is (LAND4): the newest record of this machine's naming it, separators and case aside, as the reader's
    /// <c>ReviewableTree</c> lets the newest stand for a tree. Null for no tree, or one no record of this machine's names.
    /// </summary>
    public static SessionRecord? SessionOfTree(IEnumerable<SessionRecord> records, string? tree) =>
        tree is null
            ? null
            : records
                .Where(record => !record.Teammate && record.Tree is { } named
                                 && string.Equals(SessionGroups.Normal(named), SessionGroups.Normal(tree), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(record => record.Created).ThenByDescending(record => record.Id, StringComparer.Ordinal)
                .FirstOrDefault();

    /// <summary>
    /// Whether a removal names a tree by its path, as `trees remove` always took one: rooted, or holding a separator. A
    /// session's branch (<c>daoris/…</c>), its tree's name (<c>s-…</c>) and a session's id are not paths (LAND3).
    /// </summary>
    public static bool NamesAPath(string named) =>
        !named.StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal)
        && (Path.IsPathRooted(named) || named.Contains('/') || named.Contains('\\'));
}
