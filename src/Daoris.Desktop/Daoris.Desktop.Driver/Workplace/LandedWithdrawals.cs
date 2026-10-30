namespace Daoris.Driver;

/// <summary>
/// What one withdrawal of a landing's branch did, or with <see cref="Planned"/> would do, in the driver's words (LANDRETIRE1).
/// </summary>
/// <param name="Done">The branch was taken away and its record says why.</param>
/// <param name="Message">The sentence both doors say.</param>
public sealed record LandedWithdrawal(bool Done, string Message)
{
    /// <summary>The recovery ref its commits were kept on, where it was withdrawn.</summary>
    public string? KeptRef { get; init; }

    /// <summary>A plan: <see cref="Message"/> says what would happen, and nothing was changed.</summary>
    public bool Planned { get; init; }
}

/// <summary>
/// The act that withdraws a branch a landing made, whose work the person or the steward says the line will never take (LANDRETIRE1, amending
/// D102's note on the clean-up of landing branches): the one way past the clean-up, which keeps a branch whose files still differ on the
/// line for ever. Over the trees' context, the content proof's keep and delete, and the clean-up's judgement of each recorded branch.
/// Beside <see cref="LandedCleanups.CleanLandedAsync"/>, which removes what the line holds with proof; this removes what it does not,
/// on a word, and keeps a way back.
/// </summary>
/// <remarks>
/// <para><b>Only a branch whose proof reads <see cref="LandedKind.Differs"/>,</b> or <see cref="LandedKind.Unknown"/> where git could compare
/// but not say (the person names the branch, so that is theirs to decide; one git could not even read, at no tip, is not). The line holding
/// it, a pull request that may still carry it, a tree having it checked out, commits its remote lacks, a branch of the person's own and
/// a name no landing recorded are refused: those are the clean-up's, with its proof, or not Daoris's at all. It is local only and never
/// touches a remote.</para>
///
/// <para><b>A way back first, then the delete.</b> The branch's commits go on a recovery ref (<see cref="ContentHolds.KeepDiscardedAsync"/>),
/// then the branch is deleted only while it is the commit judged, in one <c>update-ref</c> transaction
/// (<see cref="ContentHolds.DeleteJudgedAsync"/>, which reads the working trees again as it goes and puts the branch back on a late
/// checkout); a refused delete takes the ref it made away again. Never <c>git branch -D</c>, which keeps no ref.</para>
///
/// <para><b>The record keeps it as a trace</b> (<see cref="LandedBranches.Withdrawn"/>): when, why, and the ref, on that landing alone.</para>
/// </remarks>
internal sealed class LandedWithdrawals(TreeContext context, ContentHolds content, LandedCleanups cleanups)
{
    private const string ChangedSince = "it changed since it was judged, so it is kept";

    /// <summary>Withdraw the landing's branch <paramref name="branch"/> of <paramref name="repository"/>, or say what would happen.</summary>
    /// <param name="root">The repository's checkout on this machine.</param>
    /// <param name="why">The person's or the steward's reason, kept with the record.</param>
    /// <param name="plan">Say what would happen, and change nothing.</param>
    internal async Task<LandedWithdrawal> WithdrawAsync(
        string root, string repository, string branch, string why, bool plan, CancellationToken ct)
    {
        why = why.Trim();
        // A plan changes nothing, so it needs no reason yet: it says the record would keep the one it is given.
        if (why.Length == 0 && !plan)
        {
            return new(false, $"`{branch}` is not withdrawn: say why the line will never hold its work (the reason is kept with its record).");
        }

        if (LandedProofs.EntriesOf(context.Recorded.All(), repository).LastOrDefault(each => string.Equals(each.Branch, branch, StringComparison.Ordinal))
            is not { } entry)
        {
            return new(false, $"no landing of this machine's made `{branch}` in `{repository}`, so Daoris withdraws nothing: only a branch a landing made is its to take, "
                + "and a branch of yours is never.");
        }

        // One look at the checkout's line (UIFREEZE1), as the clean-up's judgement takes.
        using var look = context.OneLook();
        if (await JudgeAsync(root, repository, entry, ct).ConfigureAwait(false) is not { } judged)
        {
            return new(false, $"`{branch}` in `{repository}` is no longer the landing's branch (it is gone, or a branch of that name holds commits the landing did not make), "
                + "so Daoris withdraws nothing.");
        }

        if (Refusal(repository, entry, judged) is { } refused) return new(false, refused);

        if (plan)
        {
            return new(false,
                $"would withdraw `{branch}` from `{repository}` ({LandedWords.Clause(judged.Item)}): its commits would be kept on a recovery ref "
                + $"(`{ContentHolds.DiscardedPrefix}{branch}`, or the next number where that name is taken), and its record would say "
                + (why.Length == 0 ? "the reason you give with --why" : why))
            { Planned = true };
        }

        // Judged again, right before it goes (D88): the list is a fact about a moment.
        var now = await JudgeAsync(root, repository, entry, ct).ConfigureAwait(false);
        if (now is null || now.Tip != judged.Tip || Refusal(repository, entry, now) is not null) return new(false, $"`{branch}`: {ChangedSince}.");

        var (reference, kept) = await ContentHolds.KeepDiscardedAsync(root, branch, now.Tip, CancellationToken.None).ConfigureAwait(false);
        if (reference is null)
        {
            return new(false, $"Daoris could not keep the commits on `{branch}` on a ref of their own ({kept}), so nothing was withdrawn.");
        }

        // The branch at the commit judged is the whole of what this rests on: nothing else holds its work, which is the point.
        var stays = await content.DeleteJudgedAsync(root, branch, now.Tip, [($"refs/heads/{branch}", now.Tip)], ct).ConfigureAwait(false);
        if (stays is not null)
        {
            await ContentHolds.DropKeptAsync(root, branch, reference, now.Tip, CancellationToken.None).ConfigureAwait(false);
            return new(false, $"`{branch}` is kept: {(stays == ContentHolds.MovedSince ? ChangedSince : stays)}");
        }

        var recorded = context.Recorded.Withdrawn(entry, why, reference);
        return new(true, $"withdrew `{branch}` from `{repository}` ({LandedWords.Clause(now.Item)}): {why}. {ContentHold.KeptAt(branch, reference)}"
            + (recorded ? "" : " Its record did not take this, so the clean-up may still list the name."))
        { KeptRef = reference };
    }

    /// <summary>The entry judged over its repository's whole set, as the clean-up judges it, at the refs as they are now; null where it was not judged.</summary>
    private async Task<LandedCleanups.Judged?> JudgeAsync(string root, string repository, LandedBranch entry, CancellationToken ct)
    {
        var refs = RefSnapshot.Take(root);
        var judged = await cleanups.JudgeLandedAsync(
            root, repository, RemoteTarget.Workspace(entry.Workspace), LandedProofs.EntriesOf(context.Recorded.All(), repository),
            await LandedProofs.SessionBranchesAsync(root, ct, refs).ConfigureAwait(false), stale: null, ct, refs).ConfigureAwait(false);
        return judged.FirstOrDefault(each => string.Equals(each.Item.Branch, entry.Branch, StringComparison.Ordinal));
    }

    /// <summary>Why the judgement does not let it be withdrawn, in a sentence; null where it does.</summary>
    private static string? Refusal(string repository, LandedBranch entry, LandedCleanups.Judged judged)
    {
        var (item, branch) = (judged.Item, entry.Branch);
        var subject = $"`{branch}` in `{repository}` is not withdrawn";
        switch (item.Kind)
        {
            case LandedKind.OnLine or LandedKind.Merged or LandedKind.Inside or LandedKind.PullRequest:
                return $"{subject}: the line holds its work ({LandedWords.Clause(item)}), which `daoris-driver trees clean` removes with that proof.";
            case LandedKind.CheckedOut:
                return $"{subject}: it is checked out in a working tree, the repository's own checkout included.";
            case LandedKind.AheadOfRemote:
                return $"{subject}: it holds {item.Commits} commit(s) its remote does not have, so it was pushed and then moved; the clean-up keeps it too.";
            case LandedKind.LeanedOn:
                // Only a branch the line's proof clears reads leaned on, so the line holds its work: the clean-up takes it, once that session branch goes.
                return $"{subject}: the line holds its work, and {LandedWords.Clause(item)}; `daoris-driver trees clean` takes both with its proof.";
            case LandedKind.Unknown when judged.Tip.Length == 0:
                return $"{subject}: {item.Detail}.";
        }

        // A pull request may still carry what a plugin pushed, unless the platform said it was abandoned: the clean-up asks it, with proof.
        if ((entry.Pushed || entry.PullRequest is not null) && entry.PullRequestState?.State != PullRequestStates.Abandoned)
        {
            return $"{subject}: a pull request may still carry it ({(entry.PullRequest is { } pull ? pull : "a plugin pushed it")}); "
                + "the clean-up asks the platform and removes it with that proof, or it is withdrawn once the pull request is abandoned.";
        }

        return null;
    }
}
