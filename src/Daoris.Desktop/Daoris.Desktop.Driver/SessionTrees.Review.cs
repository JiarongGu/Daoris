using System.Globalization;

namespace Daoris.Driver;

/// <summary>Where a landed session's branch stands now, for its review (REVIEW2, D113).</summary>
public static class LandedState
{
    /// <summary>The branch stands and still holds the commit the landing made it at: its changes are read from it.</summary>
    public const string Standing = "standing";

    /// <summary>No branch of the landing's is here any more: the clean-up removed it, or someone deleted it.</summary>
    public const string Gone = "gone";

    /// <summary>A branch of that name stands and does not hold the landing's commit: rebased or replaced, so not read as this session's work.</summary>
    public const string NotOurs = "not-ours";

    /// <summary>The repository has no checkout on this machine, or its checkout is not a repository of its own.</summary>
    public const string NoCheckout = "no-checkout";
}

/// <summary>
/// Whether a gone branch's work reads on the line (REVIEW2), by WSR5's proof by content on the commit the landing made
/// it at, while git still holds that commit.
/// </summary>
/// <param name="Kind">
/// <see cref="LandedKind.OnLine"/>, <see cref="LandedKind.Merged"/>, <see cref="LandedKind.Differs"/> or
/// <see cref="LandedKind.Unknown"/> as the proof found it, or <see cref="CommitsGone"/>.
/// </param>
/// <param name="Where">On the line or merged: the form of the line it was proven on. Differs: the form compared with.</param>
/// <param name="Files">Differs: the files it changed that read otherwise on the line.</param>
/// <param name="Detail">Unknown: git's own words.</param>
public sealed record LandedReads(string Kind, string? Where, IReadOnlyList<string> Files, string? Detail)
{
    /// <summary>Git no longer holds the commit the landing made the branch at (its objects were pruned), so the proof cannot be made.</summary>
    public const string CommitsGone = "commits-gone";
}

/// <summary>A landed session's review (REVIEW2, D113): its landing, where that branch stands, and what it holds.</summary>
/// <param name="Entry">The landing record, standing or a trace.</param>
/// <param name="State">One of <see cref="LandedState"/>.</param>
/// <param name="Changes">Standing, where asked: the changes from where its work grew from up to the branch; null where git could not read them.</param>
/// <param name="Reads">Gone: whether its work reads on the line.</param>
/// <param name="Detail">Standing: why its changes could not be read, where they could not.</param>
public sealed record LandedReview(LandedBranch Entry, string State, WorkingTree.TreeDiff? Changes = null, LandedReads? Reads = null, string? Detail = null)
{
    /// <summary>
    /// Whether the session's review reads as landed: its landed branch stands, or its tree is gone. Then accepting is
    /// no door — a second landing is refused while the branch stands (D87), and there is no tree to land once it is
    /// gone — and neither is sending it back; only the hand-off is, where one applies (WSR5b).
    /// </summary>
    /// <remarks>
    /// A tree still here after its landed branch went is a session that may have carried on after its landing
    /// (WSR6's replay keeps its own commits), so its review is the tree's again, as any other.
    /// </remarks>
    public bool ReadsAsLanded(bool treeGone) => treeGone || State == LandedState.Standing;
}

/// <summary>A landed session's review in words (REVIEW2): the terminal's sentence, which the review's note says in its own catalogue.</summary>
public static class LandedReviewWords
{
    /// <summary>Where <paramref name="session"/>'s work landed, and where that branch stands now.</summary>
    public static string Describe(string session, LandedReview review)
    {
        var entry = review.Entry;
        var when = entry.LandedAt == DateTimeOffset.MinValue
            ? ""
            : $" on {entry.LandedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC";
        // Who accepted it (LAND2b, D145 point 4): said where it was the rule's switch, since a press is what the review offers.
        var by = entry.AcceptedBy == AcceptedBy.Auto ? ", accepted automatically when its quest was done" : "";
        var landed = $"`{session}` landed on `{entry.Branch}` in `{entry.Repository}`{when}{by}";
        var pullRequest = entry is { Pushed: true, PullRequest: { } pr } ? $" Its pull request: {pr}" : "";
        return review.State switch
        {
            LandedState.Standing => $"{landed}; that branch still stands.{pullRequest}",
            LandedState.NotOurs => $"{landed}; that branch no longer holds the commit the landing made it at — it was rebased or "
                + "replaced — so it is not read as this session's work.",
            LandedState.NoCheckout => $"{landed}; `{entry.Repository}` has no checkout on this machine to read it in.",
            _ => $"{landed}; that branch is gone now.{Removed(entry)}{Reads(review.Reads)}",
        };
    }

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

public sealed partial class SessionTrees
{
    /// <summary>
    /// Whether a session's tree is gone from this machine (REVIEW2): its record names none, its folder is not there, or
    /// the folder is empty — what a removal leaves where something held the folder open (D109 §5).
    /// </summary>
    public static bool TreeGone(string? tree)
    {
        if (string.IsNullOrWhiteSpace(tree) || !Directory.Exists(tree)) return true;
        try
        {
            return !Directory.EnumerateFileSystemEntries(tree).Any();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A folder nobody may list is not a tree a review can read.
            return true;
        }
    }

    /// <summary>
    /// A landed session's review (REVIEW2, D113): where its landing's branch stands in the repository's own checkout,
    /// and, while it stands, what it holds — from where its work grew from up to the branch.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Reads, and never writes.</b> Every question is a read of refs and objects (<c>rev-parse</c>,
    /// <c>merge-base</c>, <c>cat-file</c>, <c>diff</c> of two commits), so the person's checkout, its working tree and
    /// its index are untouched whatever state they are in.</para>
    ///
    /// <para>🔴 <b>The checkout is proven first</b>: git walks UP, so a registered root that is not the top of a
    /// repository of its own would answer for whatever repository encloses it (FIX-LOG). Such a root is
    /// <see cref="LandedState.NoCheckout"/>, and git is asked nothing else.</para>
    ///
    /// <para><b>A trace is gone, whatever stands under its name now</b>: the record says the landing's branch went, and
    /// a branch of that name since is someone else's. Whether its work reads on the line is WSR5's proof by content on
    /// the commit the landing made it at, while git still holds it.</para>
    /// </remarks>
    /// <param name="root">The repository's checkout on this machine, from the registry; null where it has none.</param>
    /// <param name="changes">Whether to read a standing branch's changes; the terminal and the acts need only where it stands.</param>
    public async Task<LandedReview> LandedReviewAsync(string? root, LandedBranch entry, bool changes = true, CancellationToken ct = default)
    {
        if (root is null || !await WorkingTree.IsTopLevelAsync(root, ct).ConfigureAwait(false))
        {
            return new(entry, LandedState.NoCheckout);
        }

        // The record's words become git's arguments, so each must be what it says it is.
        if (!WorkingTree.IsCommitId(entry.Tip) || !BranchName.IsValid(entry.Branch))
        {
            return new(entry, LandedState.NotOurs);
        }

        if (entry.GoneAt is not null) return new(entry, LandedState.Gone, Reads: await ReadsAsync(root, entry, ct).ConfigureAwait(false));

        var (tipCode, tipOut, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{entry.Branch}^{{commit}}"], ct).ConfigureAwait(false);
        if (tipCode != 0) return new(entry, LandedState.Gone, Reads: await ReadsAsync(root, entry, ct).ConfigureAwait(false));

        var tip = tipOut.Trim();
        var (ours, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", entry.Tip, tip], ct).ConfigureAwait(false);
        if (ours != 0) return new(entry, LandedState.NotOurs);
        if (!changes) return new(entry, LandedState.Standing);

        var from = await LandedFromAsync(root, entry, tip, ct).ConfigureAwait(false);
        if (from is null)
        {
            return new(entry, LandedState.Standing, Detail: $"git found no point where `{entry.Branch}` left its line, so there is no range to read.");
        }

        var diff = await WorkingTree.DiffBetweenAsync(root, from, tip, ct).ConfigureAwait(false);
        return diff is null
            ? new(entry, LandedState.Standing, Detail: $"git could not read `{entry.Branch}`'s changes from {from[..Math.Min(8, from.Length)]} in the repository's checkout.")
            : new(entry, LandedState.Standing, diff);
    }

    /// <summary>
    /// Where a landed branch's work grew from (WSR6): the start of the session branch it was made from, as the landing
    /// recorded it, while that is still in the branch's history; else where the branch leaves the line.
    /// </summary>
    private async Task<string?> LandedFromAsync(string root, LandedBranch entry, string tip, CancellationToken ct)
    {
        if (entry.From is { } recorded && WorkingTree.IsCommitId(recorded))
        {
            var (held, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", recorded, tip], ct).ConfigureAwait(false);
            if (held == 0)
            {
                var (_, full, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{recorded}^{{commit}}"], ct).ConfigureAwait(false);
                if (full.Trim() is { Length: > 0 } commit) return commit;
            }
        }

        var line = (await LineAsync(root, entry.Repository, RemoteTarget.Workspace(entry.Workspace), ct).ConfigureAwait(false)).Branch ?? entry.Line;
        var against = line is null ? null : await ComparableAsync(root, line, ct).ConfigureAwait(false);
        if (against is null) return null;
        var (code, fork, _) = await WorkingTree.GitAsync(root, ["merge-base", against, tip], ct).ConfigureAwait(false);
        return code == 0 && fork.Trim() is { Length: > 0 } found ? found : null;
    }

    /// <summary>Whether a gone branch's work reads on the line: WSR5's proof on the commit the landing made it at, while git holds it.</summary>
    private async Task<LandedReads> ReadsAsync(string root, LandedBranch entry, CancellationToken ct)
    {
        var (held, _, _) = await WorkingTree.GitAsync(root, ["cat-file", "-e", $"{entry.Tip}^{{commit}}"], ct).ConfigureAwait(false);
        if (held != 0) return new(LandedReads.CommitsGone, null, [], null);

        var line = (await LineAsync(root, entry.Repository, RemoteTarget.Workspace(entry.Workspace), ct).ConfigureAwait(false)).Branch ?? entry.Line;
        var proof = await ProveAsync(root, entry.Tip, line, await LineFormsAsync(root, line, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
        return new(proof.Kind, proof.Where, proof.Kind == LandedKind.Differs ? proof.Files : [], proof.Detail);
    }
}
