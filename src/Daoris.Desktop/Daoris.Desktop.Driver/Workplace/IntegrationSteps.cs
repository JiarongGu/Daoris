namespace Daoris.Driver;

/// <summary>
/// The integration run's steps over the store (INTEGRATE1e part e2, D215; the integration design §5.6): each step's start written before its
/// effect and its end after, each in one <c>BEGIN IMMEDIATE</c> transaction under the candidate's claim, and the come-up's recovery, which
/// judges a promotion by ancestry before anything is retried. Called from no production path: rows e3 and e4 make the steps' effects.
/// </summary>
/// <remarks>
/// <para><b>The rule.</b> A step's start is one write; its effect (a tree made, a replay, a gate, the line moved) is made with nothing of the
/// store held; its end is one write, with everything the step made of its candidate, its members and their requests. A crash leaves the store
/// at one of three places: no start (the step never began), a start with no end (the effect may or may not be made), or the end (the step is
/// done). Only the middle one is judged.</para>
/// <para><b>The recovery asks git, never the store alone.</b> Where the candidate has a tip, git is asked whether the line holds it
/// (<c>merge-base --is-ancestor</c>), since the line may move before its record: a promotion's <c>update-ref</c> made and its end never
/// written. Held, and the store not yet saying so, the promotion is recorded at the come-up (its open step ended <c>promoted</c>, or a
/// promotion's start and end written where none was open) and never made again. Not held, the open step ends <c>interrupted</c> and runs again
/// from its start as its next attempt, once the candidate's job holds no process, so two attempts never run at once. Where git cannot say,
/// nothing is written and the step stays as it stands, to be asked again; where the store records a promotion the line no longer holds,
/// nothing is retried. A step is idempotent from its start because its start records the line's commit it goes from, and a promotion's one
/// <c>update-ref</c> verifies that old value, so an attempt run again never moves a line that moved under it.</para>
/// </remarks>
public sealed class IntegrationSteps(IntegrationStore store)
{
    /// <summary>
    /// Run one step: its start written, then <paramref name="effect"/>, then its end as the effect answers it. An effect that throws leaves the
    /// start with no end, as a crash would: the claim's holder resumes it (<see cref="ResumeAsync"/>) at its next look, a taker at its come-up.
    /// </summary>
    /// <param name="request">The member a member's step is of.</param>
    /// <param name="lineCommit">The line's commit the step read at its start, which its effect goes from.</param>
    public async Task<IntegrationStep> RunAsync(
        IntegrationClaim claim, string step, Func<IntegrationStep, CancellationToken, Task<IntegrationStepEnd>> effect, string? request = null,
        string? lineCommit = null, CancellationToken ct = default)
    {
        var begun = store.Begin(claim, step, request, lineCommit);
        var end = await effect(begun, ct).ConfigureAwait(false);
        return store.End(claim, begun.Id, end);
    }

    /// <summary>
    /// Resume a candidate the claim holds, at the come-up or after a step's effect threw (§5.6's recovery): its promotion judged by ancestry
    /// first, in the registered checkout at <paramref name="root"/>, then its open step ended for its next attempt. What to do next is the
    /// answer's (<see cref="IntegrationResumes"/>).
    /// </summary>
    /// <exception cref="IntegrationClaimLostException">The claim no longer holds the candidate.</exception>
    public async Task<IntegrationResumed> ResumeAsync(IntegrationClaim claim, string root, CancellationToken ct = default)
    {
        var candidate = store.Candidate(claim.Candidate) ?? throw new IntegrationClaimLostException(claim, "the store holds no such candidate");
        if (!candidate.Stands)
        {
            return new IntegrationResumed(IntegrationResumes.Ended, $"candidate {candidate.Id} ended ({candidate.EndedWhy}): there is nothing to resume.")
            {
                Candidate = candidate,
            };
        }

        var open = store.OpenStep(candidate.Id);
        var (held, unread) = candidate.Tip is { } tip ? await HoldsAsync(root, candidate.Line, tip, ct).ConfigureAwait(false) : (false, null);
        if (unread is not null)
        {
            return new IntegrationResumed(IntegrationResumes.Unread,
                $"git could not say whether `{candidate.Line}` holds candidate {candidate.Id}'s tip {Short(candidate.Tip!)} ({unread}): nothing was written, "
                + "and it is asked again at the next look.")
            {
                Candidate = candidate, Step = open,
            };
        }

        if (held && candidate.PromotedAt is null)
        {
            var said = $"`{candidate.Line}` holds the candidate's tip {Short(candidate.Tip!)} by ancestry: its promotion is recorded at the come-up, and never made again.";
            var promoted = open is not null
                ? store.End(claim, open.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Promoted) { Said = said, Promoted = true })
                : await RunAsync(claim, IntegrationStepNames.Promote,
                    (_, _) => Task.FromResult(new IntegrationStepEnd(IntegrationStepOutcomes.Promoted) { Said = said, Promoted = true }), ct: ct).ConfigureAwait(false);
            return new IntegrationResumed(IntegrationResumes.Promoted, said) { Candidate = store.Candidate(candidate.Id), Step = promoted };
        }

        if (!held && candidate.PromotedAt is { } at)
        {
            return new IntegrationResumed(IntegrationResumes.Unheld,
                $"the store records candidate {candidate.Id} promoted at {Moments.Write(at)}, and `{candidate.Line}` does not hold its tip {Short(candidate.Tip ?? "")}: "
                + "nothing is retried or written, since a line moved back is the person's to settle.")
            {
                Candidate = candidate, Step = open,
            };
        }

        if (open is null)
        {
            var last = store.Steps(candidate.Id).LastOrDefault();
            return new IntegrationResumed(IntegrationResumes.Next,
                last is null ? $"candidate {candidate.Id} has begun no step." : $"candidate {candidate.Id}'s last step, {last.Step}, ended {last.Outcome}: it goes on after it.")
            {
                Candidate = candidate, Step = last,
            };
        }

        // The attempt a crash cut runs again from its start, once nothing it started is still running: the candidate's one job is ended first.
        try
        {
            await ProcessJob.EndNamedAsync(candidate.Job).ConfigureAwait(false);
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            return new IntegrationResumed(IntegrationResumes.Unread,
                $"candidate {candidate.Id}'s job could not be opened to end what its {open.Step} started ({error.Message}): nothing was written, and it is "
                + "asked again at the next look.")
            {
                Candidate = candidate, Step = open,
            };
        }

        var again = $"its {open.Step} attempt {open.Attempt} began at {Moments.Write(open.BeganAt)} and has no end"
                    + (candidate.Tip is null ? "" : held ? ", the candidate's promotion recorded" : $", and `{candidate.Line}` does not hold the candidate's tip")
                    + ": it runs again from its start.";
        var ended = store.End(claim, open.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Interrupted) { Said = again });
        return new IntegrationResumed(IntegrationResumes.Again, again) { Candidate = store.Candidate(candidate.Id), Step = ended };
    }

    /// <summary>Whether the line holds <paramref name="tip"/>, asked of git: yes, no, or git's words where it could not say.</summary>
    private static async Task<(bool Held, string? Unread)> HoldsAsync(string root, string line, string tip, CancellationToken ct)
    {
        var (code, _, err) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, $"refs/heads/{line}"], ct).ConfigureAwait(false);
        return code switch
        {
            0 => (true, null),
            1 => (false, null),
            _ => (false, ContentHolds.Said(err)),
        };
    }

    private static string Short(string commit) => TextBounds.ShortCommit(commit, 8);
}

/// <summary>What a resume found, in words, with the candidate as it now stands and the step it names.</summary>
/// <param name="Kind">One of <see cref="IntegrationResumes"/>.</param>
public sealed record IntegrationResumed(string Kind, string Said)
{
    public IntegrationCandidate? Candidate { get; init; }

    /// <summary>The step the answer is of: the promotion recorded, the attempt ended for its next, the last ended, or the open one left as it stands.</summary>
    public IntegrationStep? Step { get; init; }
}

/// <summary>What a resume answers, and what its caller does next.</summary>
public static class IntegrationResumes
{
    /// <summary>The line holds the candidate's tip and its promotion is now recorded: the caller settles.</summary>
    public const string Promoted = "promoted";

    /// <summary>The open step was cut and ended <c>interrupted</c>: the caller runs <see cref="IntegrationResumed.Step"/>'s step again from its start.</summary>
    public const string Again = "again";

    /// <summary>No step was open: the caller goes on after <see cref="IntegrationResumed.Step"/>, the last ended.</summary>
    public const string Next = "next";

    /// <summary>The candidate ended: nothing to resume.</summary>
    public const string Ended = "ended";

    /// <summary>git, or the candidate's job, could not answer: nothing was written, and the resume is asked again at the next look.</summary>
    public const string Unread = "unread";

    /// <summary>The store records a promotion the line does not hold: nothing was retried or written.</summary>
    public const string Unheld = "unheld";
}
