using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// A landed session's review (REVIEW2, D113; PART10k, out of <c>SessionTrees.Review.cs</c>): where its landing's branch stands in
/// the repository's own checkout, and what it holds.
/// </summary>
/// <remarks>
/// It names the landing's proof by its class (<see cref="LandedProofs.ProveAsync"/>, <see cref="LandedProofs.LineFormsAsync"/>; PART10l).
/// </remarks>
internal sealed class LandedReviews(TreeContext context)
{
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
    internal async Task<LandedReview> LandedReviewAsync(string? root, LandedBranch entry, bool changes = true, CancellationToken ct = default)
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
            ? new(entry, LandedState.Standing, Detail: $"git could not read `{entry.Branch}`'s changes from {TextBounds.ShortCommit(from, 8)} in the repository's checkout.")
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
                // GITAPP1a-ii: the choice Review's graph makes too (LandedGraph).
                if (ReviewChoices.LandedFrom(held, full) is { } commit) return commit;
            }
        }

        var line = (await context.LineAsync(root, entry.Repository, RemoteTarget.Workspace(entry.Workspace), ct).ConfigureAwait(false)).Branch ?? entry.Line;
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

        var line = (await context.LineAsync(root, entry.Repository, RemoteTarget.Workspace(entry.Workspace), ct).ConfigureAwait(false)).Branch ?? entry.Line;
        var proof = await LandedProofs.ProveAsync(root, entry.Tip, line, await LandedProofs.LineFormsAsync(root, line, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
        return new(proof.Kind, proof.Where, proof.Kind == LandedKind.Differs ? proof.Files : [], proof.Detail);
    }
}
