using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <param name="Where">On the line: the form of the line it is proven on (`main`, `origin/main`). Inside: the branch it is inside. Leaned on: the session branch.</param>
/// <param name="Files">Differs: the files it changed that read otherwise on the line.</param>
/// <param name="Detail">Git's own words where it could not say.</param>
/// <param name="PullRequest">The pull request a plugin answered with when it pushed the branch, where the record knows one.</param>
/// <param name="Commits">Ahead of its remote: the commits the remote lacks. Otherwise: the commits it holds that are not on the line.</param>
public sealed record LandedItem(
    string Repository, string Workspace, string Branch, string Kind, string? Where, IReadOnlyList<string> Files,
    string? Detail, string? PullRequest, int Commits)
{
    public bool Removable => Kind is LandedKind.OnLine or LandedKind.Merged or LandedKind.Inside or LandedKind.PullRequest;

    /// <summary>
    /// The latest answer about its pull request, where one is kept: what a row says the platform
    /// answered, and when.
    /// </summary>
    public PullRequestState? State { get; init; }

    /// <summary>
    /// Why a kept answer does not clear it, where one is kept and does not: the state (<c>open</c>, <c>abandoned</c>,
    /// <c>unknown</c>) or one of <see cref="PullRequestCodes"/>. Null where nothing is kept, or the answer cleared it.
    /// </summary>
    public string? StateCode { get; init; }

    /// <summary>The latest ask that failed since that answer, where one did.</summary>
    public PullRequestAskFailed? AskFailed { get; init; }

    /// <summary>
    /// Why this look did not ask about its pull request, where it would have: no plugin to ask, the
    /// plugin not ready, or the occasion's bound reached first. Null where it is asked, is not due, or its work reads on the line.
    /// </summary>
    public PullRequestNotAsked? NotAsked { get; init; }

    /// <summary>When the landing recorded this branch, the age a row says; null where a row is made from no record.</summary>
    public DateTimeOffset? LandedAt { get; init; }

    /// <summary>The quest the landing is named for, where it names one.</summary>
    public string? Quest { get; init; }

    /// <summary>
    /// Whether the service says <see cref="Quest"/> is closed (<see cref="LandedStanding.IsClosed"/>), as the caller read it once; false where it
    /// is not read, so a row never says a quest closed on no answer.
    /// </summary>
    public bool QuestClosed { get; init; }
}
