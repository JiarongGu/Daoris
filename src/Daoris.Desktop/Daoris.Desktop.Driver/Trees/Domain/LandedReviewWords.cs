using System.Globalization;

namespace Daoris.Driver;

/// <summary>A landed session's review in words (REVIEW2): the terminal's sentence, which the review's note says in its own catalogue.</summary>
public static class LandedReviewWords
{
    /// <summary>Where <paramref name="session"/>'s work landed, and where that branch stands now.</summary>
    public static string Describe(string session, LandedReview review)
    {
        var entry = review.Entry;
        // This session's own acceptance onto the branch (LAND2c): the landing that made it, or the advance its done made.
        var at = entry.AcceptedAt(session);
        var when = at == DateTimeOffset.MinValue
            ? ""
            : $" on {at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC";
        // Who accepted it (LAND2b, D145 point 4): said where it was the rule's switch, since a press is what the review offers.
        // AUTONOMY1c2's setup accepts by the rule: its record keeps the level that set it, said (ACCESSWORDS1). Only the landing's own
        // acceptance is the setup's; an advance of the branch keeps no rule.
        var by = entry.AcceptedByOf(session) != AcceptedBy.Auto ? ""
            : string.Equals(entry.Session, session, StringComparison.OrdinalIgnoreCase) && entry.Rule?.Access is { } access
                ? $", accepted by the access setup ({access})"
                : ", accepted automatically when its quest was done";
        var landed = string.Equals(entry.Session, session, StringComparison.OrdinalIgnoreCase)
            ? $"`{session}` landed on `{entry.Branch}` in `{entry.Repository}`{when}{by}"
            : $"`{session}` moved `{entry.Branch}` on in `{entry.Repository}`{when}, after `{entry.Session}`'s landing{by}";
        return review.State switch
        {
            LandedState.Standing => $"{landed}; that branch still stands.{PullRequest(entry)}",
            LandedState.NotOurs => $"{landed}; that branch no longer holds the commit the landing made it at — it was rebased or "
                + "replaced — so it is not read as this session's work.",
            LandedState.NoCheckout => $"{landed}; `{entry.Repository}` has no checkout on this machine to read it in.",
            // A branch removed on its pull request's answer says that answer in its removal, once.
            _ => $"{landed}; that branch is gone now.{Removed(entry)}{Reads(review.Reads)}"
                 + (entry.RemovedAs == LandedKind.PullRequest ? "" : PullRequest(entry, keptOnly: true)),
        };
    }

    /// <summary>
    /// Its pull request: the address a plugin answered with when it pushed, and what the plugin last answered about it with when,
    /// and a failed ask beside that (PLUGHOOK1c, D148 point 6, design §2.5), as the review's note says them.
    /// </summary>
    /// <param name="keptOnly">Say nothing where no answer and no failure is kept: a gone branch's address alone says nothing new.</param>
    private static string PullRequest(LandedBranch entry, bool keptOnly = false)
    {
        var address = entry is { Pushed: true, PullRequest: { } pr } ? pr : entry.PullRequestState?.PullRequest;
        var kept = entry.PullRequestState;
        var failed = entry.PullRequestAskFailed is { } ask ? $" {Capital(PullRequestWords.Failed(ask, again: kept is not null))}." : "";
        if (kept is null) return keptOnly ? failed : (address is null ? "" : $" Its pull request: {address}") + failed;
        return $" Its pull request: {(address is null ? "" : $"{address}, ")}{PullRequestWords.Kept(kept)}.{failed}";
    }

    private static string Capital(string words) => words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];

    /// <summary>
    /// Why a press to land a session whose review reads as landed lands nothing again (REVIEW2): the terminal's
    /// answer to <c>trees land</c>, where the review offers no Accept at all.
    /// </summary>
    public static string NotAgain(string session, LandedReview review, bool treeGone)
    {
        var why = treeGone
            ? "Nothing was landed again: its tree is gone."
            : "Nothing was landed again: a second landing is refused while that branch stands.";
        var hand = review is { State: LandedState.Standing, Entry.Pushed: false }
            ? $" `daoris-driver trees hand {session}` hands that branch to a landing plugin."
            : "";
        return $"{Describe(session, review)} {why}{hand}";
    }

    private static string Removed(LandedBranch entry) => entry.RemovedAs switch
    {
        null => "",
        LandedKind.Inside => $" The clean-up removed it inside `{entry.RemovedOn}`, whose work read on the line.",
        // PLUGHOOK1a: removed on the platform's word, confirmed by git.
        LandedKind.PullRequest => $" The clean-up removed it once its pull request completed"
            + (entry.PullRequestState?.Plugin is { } plugin ? $", as `{plugin}` answered," : "") + $" and its merge commit read on `{entry.RemovedOn}`.",
        _ => $" The clean-up removed it once its work read on `{entry.RemovedOn}`.",
    };

    private static string Reads(LandedReads? reads) => reads?.Kind switch
    {
        null => "",
        LandedKind.OnLine or LandedKind.Merged => $" Its work reads on `{reads.Where}`.",
        LandedKind.Differs => (reads.Files.Count == 1 ? " 1 file it changed reads" : $" {reads.Files.Count} files it changed read")
            + $" otherwise on `{reads.Where}`: {string.Join(", ", reads.Files.Take(3))}" + (reads.Files.Count > 3 ? $" and {reads.Files.Count - 3} more." : "."),
        LandedReads.CommitsGone => " Git no longer holds its commits here, so whether its work reads on the line cannot be said.",
        _ => $" Git could not say whether its work reads on the line: {reads.Detail}",
    };
}
