namespace Daoris.Driver;

/// <summary>What the settle judged: the step's end to write, or the reason git could not say (nothing is then written and it is asked again).</summary>
/// <param name="Left">A member is left standing (a superseded commit waits for part e4b), so the candidate is not ended.</param>
internal sealed record IntegrationSettled(IntegrationStepEnd? End, string? Unread, bool Left);

/// <summary>
/// The integration run's settle step (INTEGRATE1e part e4a, D215; the integration design §5.2 step 9, §5.7): for each promoted member, the commits
/// its branch made after its request froze judged against the promoted line (<see cref="IntegrationAfterLanding"/>), and each member settled
/// with its request closed. Contained is said and nothing is kept; new is said as offered by the rule's own <c>request</c> (§4.6); a superseded
/// commit is never dropped: its member is left standing, its request open, with a sentence that part e4b records its recovery ref and its
/// pending disposition. Reads git only: nothing is written here, and a judgement git could not make is answered, not guessed.
/// </summary>
internal static class IntegrationSettles
{
    /// <summary>
    /// The end the settle step writes for <paramref name="candidate"/>, promoted at its tip: every standing member promoted (or replayed, where a
    /// resume recorded the promotion by ancestry) judged, each member contained by one of them settled with it.
    /// </summary>
    public static async Task<IntegrationSettled> JudgeAsync(
        IntegrationStore store, string root, string line, IntegrationCandidate candidate, IReadOnlyList<IntegrationMember> standing, CancellationToken ct)
    {
        var promoted = standing.Where(each => each is { State: IntegrationMemberStates.Promoted or IntegrationMemberStates.Replayed, Replayed: not null }).ToList();
        var changes = new List<IntegrationMemberChange>();
        var closes = new List<IntegrationRequestClose>();
        var said = new List<string>();
        var left = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in promoted)
        {
            var request = store.Request(member.Request)!;
            var branch = request.Branch;
            var (read, unread) = await ReadAsync(root, candidate, member, request, ct).ConfigureAwait(false);
            if (read is null) return new IntegrationSettled(null, unread, false);
            var judged = await IntegrationAfterLanding.JudgeAsync(root, line, candidate.Tip!, read.Fork, request.Tip, read.BranchTip, read.Own, ct).ConfigureAwait(false);
            if (judged is null) return new IntegrationSettled(null, $"git could not judge the commits made on `{branch}` after its request froze: nothing was written, and it is asked again at the next look.", false);

            var contained = judged.Count(each => each.Disposition == AfterLandingDispositions.Contained);
            var fresh = judged.Count(each => each.Disposition == AfterLandingDispositions.New);
            var superseded = judged.Where(each => each.Disposition == AfterLandingDispositions.Superseded).ToList();
            if (superseded.Count == 0)
            {
                var words = judged.Count == 0
                    ? $"no commit was made on `{branch}` after its request froze."
                    : $"of {judged.Count} {Commits(judged.Count)} made on `{branch}` after its request froze, {contained} contained (the line holds them: said, never offered again) "
                      + $"and {fresh} new (offered by the rule's `request`, §4.6).";
                said.Add($"request {member.Request}: {words}");
                changes.Add(new IntegrationMemberChange(member.Request, IntegrationMemberStates.Settled) { Said = words, Ends = true });
                closes.Add(new IntegrationRequestClose(member.Request, IntegrationRequestCloses.Settled));
                continue;
            }

            var by = string.Join(", ", superseded.Select(each => TextBounds.ShortCommit(each.By ?? "", 8)).Distinct(StringComparer.Ordinal));
            var kept = $"{superseded.Count} {Commits(superseded.Count)} made on `{branch}` after its request froze {(superseded.Count == 1 ? "is" : "are")} superseded by {by} "
                       + $"({contained} contained, {fresh} new): the member stays standing with its request open and nothing is dropped; part e4b records the recovery ref "
                       + "and asks the session whether to rebase it or to let it go.";
            said.Add($"request {member.Request}: {kept}");
            changes.Add(new IntegrationMemberChange(member.Request, IntegrationMemberStates.Promoted) { Said = kept });
            left.Add(member.Request);
        }

        // A member another member's replay held settles with that member, where that one settled.
        foreach (var member in standing.Where(each => each is { State: IntegrationMemberStates.Contained, ContainedBy: not null }))
        {
            if (left.Contains(member.ContainedBy!) || !promoted.Any(each => each.Request == member.ContainedBy)) continue;
            var words = $"request {member.ContainedBy}'s replay held its work and settled: it settles with it.";
            changes.Add(new IntegrationMemberChange(member.Request, IntegrationMemberStates.Settled) { Said = words, Ends = true });
            closes.Add(new IntegrationRequestClose(member.Request, IntegrationRequestCloses.Settled));
            said.Add($"request {member.Request}: {words}");
        }

        var sentence = said.Count == 0 ? "no member was left to settle." : string.Join(" ", said);
        return new IntegrationSettled(
            new IntegrationStepEnd(left.Count > 0 ? IntegrationStepOutcomes.Held : IntegrationStepOutcomes.Done)
            {
                Said = sentence, Members = changes, Closes = closes, Ends = left.Count > 0 ? null : IntegrationCandidateEnds.Settled,
            },
            null, left.Count > 0);
    }

    private static string Commits(int count) => count == 1 ? "commit" : "commits";

    /// <summary>What a member's judgement is made from, read of git: where its branch stands now, where the unit began, and what of the line is its own work.</summary>
    private sealed record Read(string BranchTip, string Fork, IReadOnlySet<string> Own);

    private static async Task<(Read? Read, string? Unread)> ReadAsync(
        string root, IntegrationCandidate candidate, IntegrationMember member, IntegrationRequest request, CancellationToken ct)
    {
        // A branch the landing's tidy removed holds nothing after its frozen tip.
        var (branchCode, branchOut, branchErr) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{request.Branch}^{{commit}}"], ct).ConfigureAwait(false);
        if (branchCode > 1) return (null, $"git could not read `{request.Branch}` ({ContentHolds.Said(branchErr)}): nothing was written, and it is asked again at the next look.");
        var branchTip = branchCode == 0 && branchOut.Trim().Length > 0 ? branchOut.Trim() : request.Tip;

        // Where the unit began is the commit its work and the line first shared: what the line changed after it is the line's side (§5.7).
        var (forkCode, forkOut, forkErr) = await WorkingTree.GitAsync(root, ["merge-base", request.Tip, candidate.Base], ct).ConfigureAwait(false);
        if (forkCode > 1) return (null, $"git could not find where `{request.Branch}` began ({ContentHolds.Said(forkErr)}): nothing was written, and it is asked again at the next look.");
        var fork = forkCode == 0 && forkOut.Trim().Length > 0 ? forkOut.Trim() : candidate.Base;

        // The unit's own landed work: its merge commit and the commits the merge brought in, which supersede nothing (§5.7).
        var replayed = member.Replayed!;
        var (ownCode, ownOut, ownErr) = await WorkingTree.GitAsync(root, ["rev-list", replayed, "--not", $"{replayed}^1"], ct).ConfigureAwait(false);
        if (ownCode != 0) return (null, $"git could not list what the replay of `{request.Branch}` made ({ContentHolds.Said(ownErr)}): nothing was written, and it is asked again at the next look.");
        var own = ownOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (new Read(branchTip, fork, own), null);
    }
}
