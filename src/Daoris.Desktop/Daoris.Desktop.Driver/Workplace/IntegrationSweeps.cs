namespace Daoris.Driver;

/// <summary>What one candidate's sweep needs of its line: where it replays and where it promotes (the repository's registered checkout, the person's choices, the home).</summary>
internal sealed record IntegrationSweepAsks(IntegrationReplayAsk Replay, IntegrationPromoteAsk Promote);

/// <summary>What the sweep did with one standing candidate (<see cref="IntegrationSweptKinds"/>), in words.</summary>
internal sealed record IntegrationSwept(string Candidate, string Kind, string Said);

/// <summary>What a sweep did with a candidate.</summary>
internal static class IntegrationSweptKinds
{
    /// <summary>The take was refused (its run lives, it moved, it ended, or it is this run's own): nothing was written.</summary>
    public const string Refused = "refused";

    /// <summary>The caller could not say where its line is (no repository, no checkout): the candidate was left as it stood.</summary>
    public const string Unasked = "unasked";

    /// <summary>The resume found nothing to run (the candidate ended, git could not say, or the line was moved back): nothing was written.</summary>
    public const string Resumed = "resumed";

    /// <summary>The candidate was taken, resumed and gone on with as far as it goes; the words are the run's.</summary>
    public const string Went = "went";
}

/// <summary>
/// The come-up's sweep of the standing candidates (INTEGRATE1e part e4b, D215; the integration design §5.6's recovery at the come-up), called
/// from no production path yet: each candidate <see cref="IntegrationStore.Standing"/> lists is taken from its dead run
/// (<see cref="IntegrationStore.Take"/>, refused from a live one), resumed first (<see cref="IntegrationSteps.ResumeAsync"/>, which asks git
/// whether the line holds the tip before anything is retried), and gone on with from the step it names.
/// </summary>
/// <remarks>
/// <para><b>Promoted</b> goes to the settle, the tidy and the close, and the promotion is never run again: the line holds the tip, and the store
/// now says so. <b>Again</b> and <b>Next</b> go on from the step: the replay's steps through <see cref="IntegrationReplays.ContinueAsync"/>, which
/// runs each member's step again from its recorded start, then the verdict, the promotion and what follows through
/// <see cref="IntegrationPromotes"/>. <b>Unheld</b>, <b>unread</b> and <b>ended</b> write nothing: a line moved back is the person's to settle,
/// and git unable to say is asked again at the next look.</para>
/// <para>A candidate that ended is no longer standing, so the sweep never meets it.</para>
/// </remarks>
internal sealed class IntegrationSweeps(IntegrationStore store, string treesRoot)
{
    private readonly IntegrationReplays _replays = new(store, treesRoot);

    private readonly IntegrationPromotes _promotes = new(store, treesRoot);

    /// <summary>
    /// Sweep every standing candidate, oldest first, once. <paramref name="asks"/> names, for each, its line's registered checkout and the
    /// person's choices; null leaves the candidate as it stands.
    /// </summary>
    public async Task<IReadOnlyList<IntegrationSwept>> SweepAsync(Func<IntegrationCandidate, IntegrationSweepAsks?> asks, CancellationToken ct = default)
    {
        var swept = new List<IntegrationSwept>();
        foreach (var candidate in store.Standing())
        {
            if (asks(candidate) is not { } ask)
            {
                swept.Add(new IntegrationSwept(candidate.Id, IntegrationSweptKinds.Unasked, $"candidate {candidate.Id}'s line was not named: it is left as it stands."));
                continue;
            }

            var taken = store.Take(candidate.Id, candidate.ClaimGeneration);
            if (taken.Claim is not { } claim)
            {
                swept.Add(new IntegrationSwept(candidate.Id, IntegrationSweptKinds.Refused, $"candidate {candidate.Id} was not taken ({taken.Refused}): nothing was written."));
                continue;
            }

            try
            {
                swept.Add(await GoOnAsync(claim, ask, ct).ConfigureAwait(false));
            }
            catch (IntegrationClaimLostException lost)
            {
                swept.Add(new IntegrationSwept(candidate.Id, IntegrationSweptKinds.Refused, lost.Message));
            }
        }

        return swept;
    }

    private async Task<IntegrationSwept> GoOnAsync(IntegrationClaim claim, IntegrationSweepAsks ask, CancellationToken ct)
    {
        var resumed = await new IntegrationSteps(store).ResumeAsync(claim, ask.Promote.Root, ct).ConfigureAwait(false);
        switch (resumed.Kind)
        {
            case IntegrationResumes.Ended or IntegrationResumes.Unread or IntegrationResumes.Unheld:
                return new IntegrationSwept(claim.Candidate, IntegrationSweptKinds.Resumed, resumed.Said);
            case IntegrationResumes.Promoted:
                // The line holds the tip, recorded now: the promotion is never run again, and the candidate goes to its settle.
                return Went(claim, await _promotes.FinishAsync(claim, ask.Promote, ct).ConfigureAwait(false));
        }

        // Again or Next: from the step the candidate was at. The replay's steps first, where a member is still to be judged.
        if (resumed.Step?.Step is null or IntegrationStepNames.Form or IntegrationStepNames.Replay or IntegrationStepNames.Resolve or IntegrationStepNames.Conflict)
        {
            var replayed = await _replays.ContinueAsync(claim, ask.Replay, ct).ConfigureAwait(false);
            if (replayed.Kind != IntegrationReplayRuns.Replayed || replayed.Candidate is not { Stands: true })
            {
                return new IntegrationSwept(claim.Candidate, IntegrationSweptKinds.Went, $"{replayed.Kind}: {replayed.Said}");
            }
        }

        return Went(claim, await _promotes.RunAsync(claim, ask.Promote, ct).ConfigureAwait(false));
    }

    private static IntegrationSwept Went(IntegrationClaim claim, IntegrationPromoteRun run) =>
        new(claim.Candidate, IntegrationSweptKinds.Went, $"{run.Kind}: {run.Said}");
}
