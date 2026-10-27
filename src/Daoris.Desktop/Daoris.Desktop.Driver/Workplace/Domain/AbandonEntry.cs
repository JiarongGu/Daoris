namespace Daoris.Driver;

/// <summary>
/// A tree an abandon discarded, or a branch it deleted alone (design §4.2): never its path, and its tip, which brings the
/// branch back while git keeps its commits (<c>git branch &lt;branch&gt; &lt;tip&gt;</c> in its repository).
/// </summary>
/// <param name="Sessions">The sessions that worked in it, oldest first.</param>
/// <param name="Alone">A branch whose tree was already gone, deleted on its own.</param>
public sealed record AbandonedTree(
    string Repository, string Branch, string? Tip, int? Commits, int? Uncommitted, IReadOnlyList<string> Sessions, bool Alone = false);

/// <summary>A piece an abandon kept, and why (design §3.2), in words from fixed lists: <see cref="AbandonWhy"/>.</summary>
/// <param name="Piece">The piece's key (<see cref="AbandonKeys.QuestKey"/>, <see cref="AbandonKeys.SessionKey"/>, <see cref="AbandonKeys.TreeKey"/>).</param>
public sealed record AbandonKeep(string Piece, string Why)
{
    /// <summary>For a quest taken on another machine, or a teammate's session, the machine where it is known.</summary>
    public string? Machine { get; init; }

    /// <summary>For a tree whose commits are elsewhere, a ref that holds one, as git names it short.</summary>
    public string? Where { get; init; }

    /// <summary>
    /// For a piece listed to go that was kept, whether it changed since the list (true) or a step could not take it (false,
    /// with the service's or git's sentence in <see cref="Detail"/>); null for a piece the list itself kept.
    /// </summary>
    public bool? Changed { get; init; }

    /// <summary>What refused it, in the service's or git's words: for the terminal and the log of what was tried. Never a path in the record.</summary>
    public string? Detail { get; init; }
}

/// <summary>A shared decline's answer from the abandon's sync pass (design §5.2): <see cref="DeclineAnswer"/>.</summary>
public sealed record AbandonDecline(string Quest, string Answer);

/// <summary>The answers a shared decline gets from the abandon's pass (D132 point 10).</summary>
public static class DeclineAnswer
{
    /// <summary>The remote took it.</summary>
    public const string Confirmed = "confirmed";

    /// <summary>Another machine's take reached the remote first: it stands, and the decline is a conflict on the quest.</summary>
    public const string Lost = "lost";

    /// <summary>The pass did not reach the remote, or left it behind: it travels on the next pass, where the same rule applies.</summary>
    public const string Unconfirmed = "unconfirmed";
}

/// <summary>One abandon, as this machine keeps it (design §4.2): the scope, when, by which door, the reason, what went and what stayed.</summary>
/// <param name="Scope"><c>ask</c> or <c>quest</c>.</param>
/// <param name="Door"><c>screen</c> or <c>terminal</c>.</param>
/// <param name="Reason">The person's reason, verbatim, as each declined quest keeps it.</param>
public sealed record AbandonEntry(string Scope, string Id, DateTimeOffset At, string Door, string Reason)
{
    /// <summary>Each quest declined.</summary>
    public IReadOnlyList<string> Declined { get; init; } = [];

    /// <summary>Whether the ask was closed with the reason (ask scope).</summary>
    public bool Closed { get; init; }

    /// <summary>Each tree discarded with its branch, and each branch deleted alone (<see cref="AbandonedTree.Alone"/>).</summary>
    public IReadOnlyList<AbandonedTree> Trees { get; init; } = [];

    /// <summary>Each session stopped, or ended unanswered.</summary>
    public IReadOnlyList<string> Stopped { get; init; } = [];

    /// <summary>Each session archived.</summary>
    public IReadOnlyList<string> Archived { get; init; } = [];

    /// <summary>Each piece kept, and why.</summary>
    public IReadOnlyList<AbandonKeep> Stayed { get; init; } = [];

    /// <summary>Each shared decline's answer.</summary>
    public IReadOnlyList<AbandonDecline> Declines { get; init; } = [];
}
