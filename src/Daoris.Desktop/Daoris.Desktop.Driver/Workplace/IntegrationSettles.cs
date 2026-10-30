namespace Daoris.Driver;

/// <summary>
/// What the settle judged of one promoted member (INTEGRATE1e part e4b): the commits its branch made after its request froze, as
/// <see cref="IntegrationAfterLanding"/> judged them, and the branch's tip where they were judged.
/// </summary>
/// <param name="Words">What the judgement says where no commit is superseded.</param>
/// <param name="Superseded">The superseded commits, oldest first; none where the member settles with nothing to keep.</param>
internal sealed record IntegrationSettlement(
    string Request, string Branch, string BranchTip, string Words, IReadOnlyList<AfterLandingCommit> Superseded, int Contained, int Fresh);

/// <summary>What the settle judged, before anything is kept or written: its members and the members they hold, or the reason git could not say (nothing is then written and it is asked again).</summary>
internal sealed record IntegrationSettled(IReadOnlyList<IntegrationSettlement> Members, IReadOnlyList<IntegrationMember> Held, string? Unread);

/// <summary>
/// The integration run's settle step (INTEGRATE1e parts e4a and e4b, D215; the integration design §5.2 step 9, §5.7): for each promoted member,
/// the commits its branch made after its request froze judged against the promoted line (<see cref="IntegrationAfterLanding"/>), and each member
/// settled with its request closed. Contained is said and nothing is kept; new is said as offered by the rule's own <c>request</c> (§4.6); a
/// superseded commit is never dropped: its branch's work is kept on a recovery ref first (<see cref="ContentHolds.KeepDiscardedAsync"/>), then
/// its disposition is written, pending, in the same end that settles the member, so a crash between them leaves the ref and no disposition,
/// which the rerun repairs by finding the ref it made. A member whose ref cannot be made is not settled and the step ends <c>held</c>.
/// </summary>
internal static class IntegrationSettles
{
    /// <summary>
    /// The judgement of <paramref name="candidate"/>, promoted at its tip: every standing member promoted (or replayed, where a resume recorded
    /// the promotion by ancestry) judged, each member contained by one of them held to settle with it. Reads git only.
    /// </summary>
    public static async Task<IntegrationSettled> JudgeAsync(
        IntegrationStore store, string root, string line, IntegrationCandidate candidate, IReadOnlyList<IntegrationMember> standing, CancellationToken ct)
    {
        var promoted = standing.Where(each => each is { State: IntegrationMemberStates.Promoted or IntegrationMemberStates.Replayed, Replayed: not null }).ToList();
        var members = new List<IntegrationSettlement>();
        foreach (var member in promoted)
        {
            var request = store.Request(member.Request)!;
            var branch = request.Branch;

            // A merge rule's press (INTEGRATE1g): nothing after it is the request's; the session's tree follows the merge as the landing says.
            if (request.Route == IntegrationRoutes.Merge)
            {
                members.Add(new IntegrationSettlement(member.Request, branch, request.Tip, "merged on the person's press.", [], 0, 0));
                continue;
            }

            var (read, unread) = await ReadAsync(root, candidate, member, request, ct).ConfigureAwait(false);
            if (read is null) return new IntegrationSettled([], [], unread);
            var judged = await IntegrationAfterLanding.JudgeAsync(root, line, candidate.Tip!, read.Fork, request.Tip, read.BranchTip, read.Own, ct).ConfigureAwait(false);
            if (judged is null) return new IntegrationSettled([], [], $"git could not judge the commits made on `{branch}` after its request froze: nothing was written, and it is asked again at the next look.");

            var contained = judged.Count(each => each.Disposition == AfterLandingDispositions.Contained);
            var fresh = judged.Count(each => each.Disposition == AfterLandingDispositions.New);
            var words = judged.Count == 0
                ? $"no commit was made on `{branch}` after its request froze."
                : $"of {judged.Count} {Commits(judged.Count)} made on `{branch}` after its request froze, {contained} contained (the line holds them: said, never offered again) "
                  + $"and {fresh} new (offered by the rule's `request`, §4.6).";
            members.Add(new IntegrationSettlement(
                member.Request, branch, read.BranchTip, words, [.. judged.Where(each => each.Disposition == AfterLandingDispositions.Superseded)], contained, fresh));
        }

        // A member another member's replay held settles with that member, where that one settles.
        var held = standing.Where(each => each is { State: IntegrationMemberStates.Contained, ContainedBy: not null }
                                          && promoted.Any(by => by.Request == each.ContainedBy)).ToList();
        return new IntegrationSettled(members, held, null);
    }

    /// <summary>
    /// The settle step's end for <paramref name="settled"/>: each superseded member's work kept on a recovery ref first, then every member's
    /// settling and each superseded commit's pending disposition as the one end. A member whose ref git would not make stays standing, said why,
    /// and the step ends <c>held</c> (tried again at the next look); the others are settled. Never ends the candidate: its tidy and close follow.
    /// </summary>
    /// <param name="afterKeep">A test's seam, called once every ref is kept and before the end is returned (the settle effect's own point).</param>
    public static async Task<IntegrationStepEnd> EndAsync(string root, IntegrationSettled settled, Func<Task>? afterKeep, CancellationToken ct)
    {
        var changes = new List<IntegrationMemberChange>();
        var closes = new List<IntegrationRequestClose>();
        var dispositions = new List<IntegrationDisposition>();
        var said = new List<string>();
        var left = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in settled.Members)
        {
            if (member.Superseded.Count == 0)
            {
                said.Add($"request {member.Request}: {member.Words}");
                changes.Add(new IntegrationMemberChange(member.Request, IntegrationMemberStates.Settled) { Said = member.Words, Ends = true });
                closes.Add(new IntegrationRequestClose(member.Request, IntegrationRequestCloses.Settled));
                continue;
            }

            // The branch's work is on a ref of its own before anything else is written: never settled, never dropped, without it.
            var (reference, why) = await ContentHolds.KeepDiscardedAsync(root, member.Branch, member.BranchTip, ct).ConfigureAwait(false);
            var by = string.Join(", ", member.Superseded.Select(each => TextBounds.ShortCommit(each.By ?? "", 8)).Distinct(StringComparer.Ordinal));
            var count = $"{member.Superseded.Count} {Commits(member.Superseded.Count)}";
            if (reference is null)
            {
                var stays = $"{count} made on `{member.Branch}` after its request froze {(member.Superseded.Count == 1 ? "is" : "are")} superseded by {by}, "
                            + $"but git would not keep the branch's work on a recovery ref ({why}): the member stays standing with its request open, and nothing is dropped.";
                said.Add($"request {member.Request}: {stays}");
                changes.Add(new IntegrationMemberChange(member.Request, IntegrationMemberStates.Promoted) { Said = stays });
                left.Add(member.Request);
                continue;
            }

            var kept = $"{count} made on `{member.Branch}` after its request froze {(member.Superseded.Count == 1 ? "is" : "are")} superseded by {by} "
                       + $"({member.Contained} contained, {member.Fresh} new), kept on `{reference}`: each is a pending disposition, offered to the session as *superseded* "
                       + "or *rebase it* and visible until one answers; nothing is discarded.";
            said.Add($"request {member.Request}: {kept}");
            changes.Add(new IntegrationMemberChange(member.Request, IntegrationMemberStates.Settled) { Said = kept, Ends = true });
            closes.Add(new IntegrationRequestClose(member.Request, IntegrationRequestCloses.Settled));
            dispositions.AddRange(member.Superseded.Select(each => new IntegrationDisposition(
                member.Request, each.Sha, reference, each.By ?? "", each.Files, each.Subject)));
        }

        if (afterKeep is not null) await afterKeep().ConfigureAwait(false);

        foreach (var member in settled.Held.Where(each => !left.Contains(each.ContainedBy!)))
        {
            var words = $"request {member.ContainedBy}'s replay held its work and settled: it settles with it.";
            changes.Add(new IntegrationMemberChange(member.Request, IntegrationMemberStates.Settled) { Said = words, Ends = true });
            closes.Add(new IntegrationRequestClose(member.Request, IntegrationRequestCloses.Settled));
            said.Add($"request {member.Request}: {words}");
        }

        return new IntegrationStepEnd(left.Count > 0 ? IntegrationStepOutcomes.Held : IntegrationStepOutcomes.Done)
        {
            Said = said.Count == 0 ? "no member was left to settle." : string.Join(" ", said),
            Members = changes,
            Closes = closes,
            Dispositions = dispositions,
        };
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
