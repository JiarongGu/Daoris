namespace Daoris.Knowledge;

/// <summary>
/// The driver's post of a finished tree's record (KEEPSIZE1b, the keep-size design §2.2): the manifest's facts and the commits read from
/// the tip. Keyed by the tree's set-aside identity, <c>(workspace, repository, folder)</c>, where the folder is <c>&lt;tree&gt;-&lt;moment&gt;</c>.
/// </summary>
/// <param name="Tree">The tree's path before the move: the sessions whose row names it are the record's.</param>
/// <param name="Moved">When the tree was moved aside: only a session made before it is the record's.</param>
/// <param name="Commits">The tree's commits, oldest first; null where the tip no longer read, and <paramref name="CommitsUnread"/> says why.</param>
/// <param name="Whole">Whether <c>whole</c> was set at the move.</param>
public sealed record WorkRecordPost(
    string Workspace, string Repository, string Folder, string Tree, DateTimeOffset Moved, string? Unit, string Branch, string? Line,
    WorkRecordHold Hold, string Tip, string? GrewFrom, string? Ref, IReadOnlyList<WorkRecordCommit>? Commits, string? CommitsUnread,
    WorkRecordLeft Left, bool Whole)
{
    /// <summary>Its landing, where one could be established before the move; null with <see cref="LandingWhy"/> where none could.</summary>
    public WorkRecordLanding? Landing { get; init; }

    /// <summary>Why no landing is named.</summary>
    public string? LandingWhy { get; init; }
}
