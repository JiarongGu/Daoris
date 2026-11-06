using System.Globalization;

namespace Daoris.Driver;

/// <summary>The clauses an inventory says its proofs, guards and times in.</summary>
internal static class InventoryWords
{
    /// <summary>
    /// Whether a guard's hold ends by itself: a session holding its tree ends, a folder held open is let go, a start finishes, git
    /// and the service answer again. Another repository, a link, a hidden change, a refusal or uncommitted work stays until someone
    /// settles it, so nothing promises that the next try takes it.
    /// </summary>
    internal static bool Frees(string why) => why is SweepKind.InUse or TidyKept.Busy or TidyKept.Starting or TidyKept.Sessions or TidyKept.Unread;

    /// <summary>A guard's code, as a clause after "since": the look's tidy's codes, and the press's two kinds that keep a tree.</summary>
    internal static string KeptSaid(string why) => why switch
    {
        SweepKind.InUse => "a session running or waiting holds its tree",
        SweepKind.Dirty => "its tree holds uncommitted or untracked work",
        TidyKept.Busy => "something on this machine holds its tree's folder open",
        TidyKept.Hidden => "a tracked path in its tree hides its changes",
        TidyKept.Nested => "its tree holds another repository",
        TidyKept.Linked => "its tree is reached through a link",
        TidyKept.Refused => "its removal did not happen: its tree is locked, git would not let its record go, or the branch moved",
        TidyKept.Unread => "git could not read one of its guards",
        TidyKept.Starting => "a session was starting in one of the repository's trees",
        TidyKept.Sessions => "the service did not say which sessions run",
        _ => $"the press's judgement keeps it ({why})",
    };

    /// <summary>What a proof's code says it found, as the end judgement says it.</summary>
    internal static string ProofSaid(string? by, string? on) => by switch
    {
        EndBy.Merged => $"every commit of it is on `{on}`",
        EndBy.Empty => $"it holds nothing `{on}` lacks",
        EndBy.OnLine => $"its files read on `{on}` as it left them",
        EndBy.PullRequest => $"its pull request completed and its merge commit is on `{on}`",
        EndBy.Landed => $"its work is on `{on}`, which the line holds",
        // After the merge: a carried fact rests on the form of the line its merge commit is on; one kept before named the carrier's
        // branch. Neither is said as the pull request, so both read as they are.
        EndBy.Carried => "a completed pull request carried it and its merge commit is on the line",
        EndBy.Inside => $"it is inside `{on}`, whose work is on the line",
        EndBy.Tidied => $"its workflow's tidy ended it once `{on}`'s pull request merged",
        _ => $"{by} on `{on}`",
    };

    internal static string Minute(DateTimeOffset at) => at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
