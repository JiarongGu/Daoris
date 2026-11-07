using Microsoft.Data.Sqlite;
using static Daoris.Driver.IntegrationRows;
using static Daoris.Driver.IntegrationStore;
using static Daoris.Driver.IntegrationStore;

namespace Daoris.Driver;

/// <summary>The candidate's steps in the family: a stale claim taken up, a step begun before its effect and ended after it, each under the claim's check.</summary>
internal sealed class IntegrationStepKeeping(IntegrationStore store)
{
    private DesktopStore Writer() => store.Writer();

    private string Now() => store.Now();

    private string Run => store.Run;

    private Func<StoreRun, bool> _alive => store._alive;

    /// <summary>
    /// Take up a candidate whose claim's run is dead by the lease's test (as the move record's take), in one guarded update that writes
    /// this store's run and raises the generation: refused, nothing written, where the candidate is gone or ended, where its claim is this
    /// run's own, where the generation moved since <paramref name="generation"/> was read, or where its run lives or cannot be told dead.
    /// </summary>
    public IntegrationTaken Take(string candidate, long generation)
    {
        try
        {
            using var store = Writer();
            return store.Write(Family, write =>
            {
                var found = CandidateIn(write, candidate) ?? throw Refused(null, IntegrationTakeRefusals.Gone);
                if (!found.Stands) throw Refused(found, IntegrationTakeRefusals.Ended);
                if (string.Equals(found.ClaimRun, Run, StringComparison.Ordinal)) throw Refused(found, IntegrationTakeRefusals.Own);
                if (found.ClaimGeneration != generation) throw Refused(found, IntegrationTakeRefusals.Moved);

                // A run the store holds no row of cannot be told dead, so it holds, as a start that cannot be read does.
                if (RunIn(write, found.ClaimRun) is not { } holder || _alive(holder)) throw Refused(found, IntegrationTakeRefusals.Live);
                var taken = write.Execute(
                    """
                    UPDATE integration_candidates SET claim_run = $mine, claim_generation = claim_generation + 1
                    WHERE id = $id AND claim_run = $run AND claim_generation = $generation AND ended_at IS NULL
                    """,
                    ("$mine", Run), ("$id", candidate), ("$run", found.ClaimRun), ("$generation", generation));
                if (taken != 1) throw Refused(found, IntegrationTakeRefusals.Moved);
                var now = CandidateIn(write, candidate)!;
                return new IntegrationTaken(now, new IntegrationClaim(candidate, Run, now.ClaimGeneration), null);
            });
        }
        catch (Unwritten unwritten)
        {
            return (IntegrationTaken)unwritten.Answer!;
        }

        static Unwritten Refused(IntegrationCandidate? found, string why) => new(new IntegrationTaken(found, null, why));
    }

    // ——— A superseded member's pending dispositions.

    /// <summary>
    /// A step's start, written before its effect, in one transaction under the claim's check: its next attempt, the member it is of and
    /// the line's commit it read. Refused while another step of the candidate is open, which must end first (a resume ends a cut one), and
    /// for an ended candidate.
    /// </summary>
    /// <exception cref="ArgumentException">A step no candidate has.</exception>
    /// <exception cref="IntegrationClaimLostException">The claim no longer holds the candidate.</exception>
    /// <exception cref="IntegrationRefusedException">A step is open, the candidate ended, or the member is not its own.</exception>
    public IntegrationStep Begin(IntegrationClaim claim, string step, string? request = null, string? lineCommit = null)
    {
        IntegrationStore.Known(step, IntegrationStepNames.All, "step");
        using var store = Writer();
        return store.Write(Family, write =>
        {
            var candidate = Claimed(write, claim);
            if (!candidate.Stands) throw new IntegrationRefusedException(claim.Candidate, $"it ended ({candidate.EndedWhy}) and takes no step");
            if (request is not null && MemberIn(write, claim.Candidate, request) is not { Stands: true })
            {
                throw new IntegrationRefusedException(claim.Candidate, $"request {request} is no standing member of it");
            }

            try
            {
                return InsertStep(write, claim, step, request, lineCommit, Now());
            }
            catch (SqliteException error) when (error.SqliteExtendedErrorCode == UniqueRefused)
            {
                var open = OpenStepIn(write, claim.Candidate);
                throw new IntegrationRefusedException(
                    claim.Candidate,
                    $"its {open?.Step} begun at {Moments.Write(open?.BeganAt)} has no end, and a candidate runs one step at a time: a step a crash cut is the resume's to judge");
            }
        });
    }

    /// <summary>
    /// A step's end, written after its effect, in one transaction under the claim's check: its outcome and words, the candidate's tip,
    /// its promotion, its members' changes, the requests it closes and the candidate's end, all or none. An end written again is the first
    /// end's, and changes nothing.
    /// </summary>
    /// <exception cref="ArgumentException">An outcome, a member's state, a close or an end no step writes.</exception>
    /// <exception cref="IntegrationClaimLostException">The claim no longer holds the candidate.</exception>
    /// <exception cref="IntegrationRefusedException">The step is not the candidate's, or a change names what the candidate does not hold: nothing is written.</exception>
    public IntegrationStep End(IntegrationClaim claim, long step, IntegrationStepEnd end)
    {
        IntegrationStore.Known(end.Outcome, IntegrationStepOutcomes.All, "outcome");
        foreach (var change in end.Members) IntegrationStore.Known(change.State, IntegrationMemberStates.All, "member's state");
        foreach (var close in end.Closes) IntegrationStore.Known(close.Why, IntegrationRequestCloses.All, "request's close");
        if (end.Ends is { } ends) IntegrationStore.Known(ends, IntegrationCandidateEnds.All, "candidate's end");
        try
        {
            using var store = Writer();
            return store.Write(Family, write =>
            {
                Claimed(write, claim);
                var found = StepIn(write, step);
                if (found is null || !string.Equals(found.Candidate, claim.Candidate, StringComparison.Ordinal))
                {
                    throw new IntegrationRefusedException(claim.Candidate, $"it has no step {step}");
                }

                if (!found.Open) throw new Unwritten(found);
                var at = Now();
                Fenced(write, claim,
                    "UPDATE integration_steps SET ended_at = $at, outcome = $outcome, said = $said WHERE id = $step AND ended_at IS NULL",
                    ("$at", at), ("$outcome", end.Outcome), ("$said", end.Said), ("$step", step));
                if (end.Tip is { } tip) Fenced(write, claim, "UPDATE integration_candidates SET tip = $tip WHERE id = $candidate", ("$tip", tip));
                if (end.Promoted)
                {
                    Fenced(write, claim, "UPDATE integration_candidates SET promoted_at = COALESCE(promoted_at, $at) WHERE id = $candidate", ("$at", at));
                }

                foreach (var change in end.Members)
                {
                    if (MemberIn(write, claim.Candidate, change.Request) is not { Stands: true })
                    {
                        throw new IntegrationRefusedException(claim.Candidate, $"request {change.Request} is no standing member of it");
                    }

                    Fenced(write, claim,
                        """
                        UPDATE integration_members
                        SET state = $state, replayed = COALESCE($replayed, replayed), contained_by = COALESCE($containedBy, contained_by),
                            said = COALESCE($said, said), ended_at = CASE WHEN $ends THEN $at ELSE ended_at END
                        WHERE candidate = $candidate AND request = $request AND ended_at IS NULL
                        """,
                        ("$state", change.State), ("$replayed", change.Replayed), ("$containedBy", change.ContainedBy), ("$said", change.Said),
                        ("$ends", change.Ends), ("$at", at), ("$request", change.Request));
                }

                foreach (var close in end.Closes)
                {
                    if (MemberIn(write, claim.Candidate, close.Request) is null || RequestIn(write, close.Request) is not { ClosedAt: null })
                    {
                        throw new IntegrationRefusedException(claim.Candidate, $"request {close.Request} is no open request of its members");
                    }

                    Fenced(write, claim,
                        "UPDATE integration_requests SET closed_at = $at, closed_why = $why WHERE id = $request AND closed_at IS NULL",
                        ("$at", at), ("$why", close.Why), ("$request", close.Request));
                }

                // A superseded commit's disposition is visible from the moment its member settles, in the same transaction.
                foreach (var disposition in end.Dispositions)
                {
                    if (MemberIn(write, claim.Candidate, disposition.Request) is null)
                    {
                        throw new IntegrationRefusedException(claim.Candidate, $"request {disposition.Request} is no member of it, so it has no disposition to keep");
                    }

                    write.Execute(
                        """
                        INSERT INTO integration_dispositions (request, commit_id, candidate, reference, by_commit, files, subject, at)
                        VALUES ($request, $commit, $candidate, $reference, $by, $files, $subject, $at)
                        ON CONFLICT (request, commit_id) DO NOTHING
                        """,
                        ("$request", Required(disposition.Request, "its request")), ("$commit", Required(disposition.Commit, "its commit")), ("$candidate", claim.Candidate),
                        ("$reference", Required(disposition.Reference, "its recovery ref")), ("$by", Required(disposition.By, "the commit that superseded it")),
                        ("$files", FilesText(disposition.Files)), ("$subject", disposition.Subject), ("$at", at));
                }

                if (end.Ends is { } why)
                {
                    Fenced(write, claim, "UPDATE integration_candidates SET ended_at = $at, ended_why = $why WHERE id = $candidate AND ended_at IS NULL",
                        ("$at", at), ("$why", why));
                    write.Execute(
                        "UPDATE integration_members SET ended_at = $at WHERE candidate = $candidate AND ended_at IS NULL",
                        ("$at", at), ("$candidate", claim.Candidate));
                }

                return StepIn(write, step)!;
            });
        }
        catch (Unwritten unwritten)
        {
            return (IntegrationStep)unwritten.Answer!;
        }
    }

    // ——— The writes' own parts.

    /// <summary>One attempt's start: its attempt is one more than the step's attempts for that member so far.</summary>
    internal static IntegrationStep InsertStep(DesktopWrite write, IntegrationClaim claim, string step, string? request, string? lineCommit, string at)
    {
        var attempts = write.Scalar<long>(
            "SELECT COUNT(*) FROM integration_steps WHERE candidate = $candidate AND step = $step AND request IS $request",
            ("$candidate", claim.Candidate), ("$step", step), ("$request", request));
        write.Execute(
            """
            INSERT INTO integration_steps (candidate, step, attempt, claim_run, claim_generation, began_at, request, line_commit)
            VALUES ($candidate, $step, $attempt, $run, $generation, $at, $request, $lineCommit)
            """,
            ("$candidate", claim.Candidate), ("$step", step), ("$attempt", attempts + 1), ("$run", claim.Run), ("$generation", claim.Generation),
            ("$at", at), ("$request", request), ("$lineCommit", lineCommit));
        return StepIn(write, write.Scalar<long>("SELECT last_insert_rowid()"))!;
    }

    /// <summary>The claim's check, inside the write's transaction: the candidate is there and its run and generation are the claim's, which is this store's run.</summary>
    private IntegrationCandidate Claimed(DesktopWrite write, IntegrationClaim claim)
    {
        if (!string.Equals(claim.Run, Run, StringComparison.Ordinal)) throw new IntegrationClaimLostException(claim, $"this store writes under run {Run}");
        var candidate = CandidateIn(write, claim.Candidate) ?? throw new IntegrationClaimLostException(claim, "the store holds no such candidate");
        if (!string.Equals(candidate.ClaimRun, claim.Run, StringComparison.Ordinal) || candidate.ClaimGeneration != claim.Generation)
        {
            throw new IntegrationClaimLostException(claim, $"run {candidate.ClaimRun} holds it at generation {candidate.ClaimGeneration}");
        }

        return candidate;
    }

    /// <summary>An update of the candidate's rows under its claim, named in the update's own condition, which must change exactly one row.</summary>
    private static void Fenced(DesktopWrite write, IntegrationClaim claim, string update, params (string Name, object? Value)[] values)
    {
        var changed = write.Execute(
            update + " AND EXISTS (SELECT 1 FROM integration_candidates WHERE id = $candidate AND claim_run = $claimRun AND claim_generation = $claimGeneration)",
            [.. values, ("$candidate", claim.Candidate), ("$claimRun", claim.Run), ("$claimGeneration", claim.Generation)]);
        if (changed != 1) throw new IntegrationClaimLostException(claim, "its row changed under the write");
    }
}
