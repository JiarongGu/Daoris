namespace Daoris.Driver;

/// <summary>What pulling a repository's line would do, or found (WSR6). Only a fast-forward moves anything.</summary>
public static class PullKind
{
    /// <summary>The local line would move forward to origin's, and nothing of its own would be lost.</summary>
    public const string FastForward = "fast-forward";

    /// <summary>The local line already holds origin's.</summary>
    public const string UpToDate = "up-to-date";

    /// <summary>The local line holds commits origin lacks: nothing to pull, and Daoris never pushes.</summary>
    public const string Ahead = "ahead";

    /// <summary>Each holds commits the other lacks: a fast-forward cannot take it, and nothing else is Daoris's to do.</summary>
    public const string Diverged = "diverged";

    /// <summary>The repository's own checkout is on the line, with uncommitted work.</summary>
    public const string Dirty = "dirty";

    /// <summary>The line is checked out in a working tree that is not the repository's own checkout.</summary>
    public const string CheckedOut = "checked-out";

    /// <summary>There is no <c>origin/&lt;line&gt;</c> here to pull from.</summary>
    public const string NoRemote = "no-remote";

    /// <summary>There is no local branch of the line: nothing to move, and the line is read from origin.</summary>
    public const string NoLocal = "no-local";

    /// <summary>No line is set and git names none.</summary>
    public const string NoLine = "no-line";

    /// <summary>Git could not say.</summary>
    public const string Unknown = "unknown";
}

/// <param name="From">The local line's commit, where it has one.</param>
/// <param name="To">Origin's commit it would move to, where there is one.</param>
/// <param name="Commits">Fast-forward: the commits it would take. Ahead: those origin lacks. Diverged: those the local line lacks.</param>
/// <param name="Fetch">Why the fetch did not happen, in git's words, or null where it did or none was asked for.</param>
/// <param name="Detail">Git's own words where it could not say.</param>
public sealed record LinePull(
    string Repository, string Workspace, string? Line, string Kind, string? From, string? To, int Commits, string? Fetch, string? Detail)
{
    public bool Moves => Kind == PullKind.FastForward;

    /// <summary>
    /// Where the fetch failed, when this checkout last heard from origin (WSR7): what the row is judged against is what
    /// origin said then. The newer of <c>FETCH_HEAD</c>'s time, where it holds a fetch, and when <c>origin/&lt;line&gt;</c>
    /// last moved here; null where neither says, which is never.
    /// </summary>
    public DateTimeOffset? LastFetch { get; init; }

    /// <summary>Where the fetch failed, how origin is reached, from its address: `ssh`, `https`, `http`, `git` or `file` (WSR7).</summary>
    public string? Reach { get; init; }
}

/// <summary>What bringing one branch up to date would do, or found (WSR6). Only a replay moves anything.</summary>
public static class RebaseKind
{
    /// <summary>Its own commits would be replayed onto the line, cutting at the commit it grew from.</summary>
    public const string Replay = "replay";

    /// <summary>It already holds the line's tip.</summary>
    public const string UpToDate = "up-to-date";

    /// <summary>A session still running or waiting holds its tree.</summary>
    public const string InUse = "in-use";

    /// <summary>Its tree has uncommitted work.</summary>
    public const string Dirty = "dirty";

    /// <summary>It is checked out in a working tree that is not a session's own: the person's checkout, or a landing's branch anywhere.</summary>
    public const string CheckedOut = "checked-out";

    /// <summary>It is on its remote: replaying it would need a force push, which is never Daoris's.</summary>
    public const string Pushed = "pushed";

    /// <summary>It grew from a commit beyond the line — the step before's — whose work has not reached the line.</summary>
    public const string Waits = "grew-from-unlanded";

    /// <summary>Git could not say.</summary>
    public const string Unknown = "unknown";
}

/// <summary>How the commit a branch's own work starts after was found (WSR6).</summary>
public static class CutBy
{
    /// <summary>It grew from the line: where it leaves the line.</summary>
    public const string Line = "line";

    /// <summary>The commit its record says it started at, beyond the line, whose work reached the line.</summary>
    public const string Record = "record";

    /// <summary>No record: the newest commit of it whose work reads on the line.</summary>
    public const string Content = "content";
}

/// <param name="Landed">A branch a landing made and recorded (WSR5), rather than a session's own.</param>
/// <param name="Onto">The line it is brought up to.</param>
/// <param name="Cut">The commit its own commits start after, where it is replayed; null otherwise.</param>
/// <param name="CutBy">How that commit was found (<see cref="Driver.CutBy"/>).</param>
/// <param name="GrewFrom">The step before's branch it started on, where its record knows one.</param>
/// <param name="Commits">Replay: its own commits. Pushed and moved since: the commits its remote lacks.</param>
/// <param name="Detail">Git's own words where it could not say.</param>
public sealed record RebaseItem(
    string Repository, string Workspace, string Branch, bool Landed, string Kind, string? Onto, string? Cut, string? CutBy,
    string? GrewFrom, int Commits, string? Detail)
{
    public bool Replays => Kind == RebaseKind.Replay;

    /// <summary>
    /// Replay: how many of its commits the line already holds, which the replay leaves out (LANDSYNC3b, D87's 2026-10-15 amendment
    /// point 4 by analogy): those the cut moved past and those it skips, beside <see cref="Commits"/>, what it carries. A run the walk
    /// by content moved the cut past is said by <see cref="CutBy"/> instead. An added field: a row from before reads 0.
    /// </summary>
    public int LeftOut { get; init; }

    /// <summary>A landed branch's latest answer about its pull request, as <see cref="LandedItem.State"/> (PLUGHOOK1c, design §2.5).</summary>
    public PullRequestState? State { get; init; }

    /// <summary>Why that answer does not clear it, as <see cref="LandedItem.StateCode"/>.</summary>
    public string? StateCode { get; init; }

    /// <summary>The latest ask that failed since that answer, as <see cref="LandedItem.AskFailed"/>.</summary>
    public PullRequestAskFailed? AskFailed { get; init; }

    /// <summary>Why the look did not ask about its pull request, where it would have, as <see cref="LandedItem.NotAsked"/>.</summary>
    public PullRequestNotAsked? NotAsked { get; init; }
}

/// <summary>A repository with a checkout here, and whether it holds a branch of Daoris's (WSR7, D112).</summary>
/// <param name="Holds">A session branch (`daoris/…`), or a branch a landing recorded that still stands.</param>
public sealed record SyncRepository(string Repository, string Workspace, bool Holds);

/// <summary>
/// Which repositories bringing up to date takes (WSR7, D112): by default those holding a branch of Daoris's, and every
/// other one only where the person includes it — all of them (`--all`), or by name (`--repository`, the screen's tick).
/// </summary>
public sealed record SyncScope
{
    /// <summary>The default: the repositories holding a branch of Daoris's, and no other.</summary>
    public static SyncScope Held { get; } = new();

    /// <summary>Every repository with a checkout here.</summary>
    public static SyncScope Everything { get; } = new() { All = true };

    /// <summary>The default and the repositories named, whatever their case.</summary>
    public static SyncScope Named(IEnumerable<string> repositories) =>
        new() { Also = repositories.ToHashSet(StringComparer.OrdinalIgnoreCase) };

    public bool All { get; init; }

    /// <summary>Repositories taken beyond the default, compared without case.</summary>
    public IReadOnlySet<string> Also { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool Includes(SyncRepository repository) => repository.Holds || All || Also.Contains(repository.Repository);

    /// <summary>
    /// This scope and each repository a row the person saw listed names (`repository:branch`, the key a press takes): the
    /// press acts on what it was shown, so a repository included at the look is included at the press.
    /// </summary>
    public SyncScope Listed(IReadOnlySet<string>? only) => only is null ? this : this with
    {
        Also = Also.Concat(only.Select(key => key.IndexOf(':') is var colon and > 0 ? key[..colon] : key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase),
    };
}

/// <summary>Bringing repositories up to date (WSR6), listed first: each line's pull, each branch's replay, and the landed branches that go.</summary>
public sealed record SyncPlan(IReadOnlyList<LinePull> Lines, IReadOnlyList<RebaseItem> Rebases, IReadOnlyList<LandedItem> Deletes)
{
    /// <summary>The repositories the look took (WSR7, D112), each with whether it holds a branch of Daoris's.</summary>
    public IReadOnlyList<SyncRepository> Looked { get; init; } = [];

    /// <summary>Every other repository with a checkout here: not fetched, not judged, listed for the person to include.</summary>
    public IReadOnlyList<SyncRepository> Apart { get; init; } = [];
}

/// <summary>What the press did with one repository's line.</summary>
public sealed record PullResult(LinePull Pull, bool Moved, string Message);

/// <summary>What the press did with one branch.</summary>
public sealed record RebaseResult(RebaseItem Item, bool Replayed, string Message);

/// <summary>What one press of bringing up to date did, all three steps.</summary>
public sealed record SyncDone(IReadOnlyList<PullResult> Lines, IReadOnlyList<RebaseResult> Rebases, IReadOnlyList<LandedResult> Deletes)
{
    /// <summary>The repositories with a checkout here the press did not take (WSR7, D112): nothing of them was touched.</summary>
    public IReadOnlyList<SyncRepository> Apart { get; init; } = [];
}
