namespace Daoris.Driver;

/// <summary>The words a promotion's steps and answers say, and the ends a hand back or a lapsed authority writes. Nothing here moves a ref.</summary>
internal static class IntegrationPromoteWords
{
    internal static string Said(IntegrationVerdict verdict, string otherwise) => verdict.Why ?? verdict.Said ?? otherwise;

    /// <summary>What a passing verdict says in the promotion's words: the gates passed (flakes read as passes and said), or the confirm run's pass the owner's rule chose.</summary>
    internal static string Words(IntegrationVerdict verdict, LandingIntegrate policy)
    {
        if (policy.Gates == LandingIntegrate.NoGates) return "the line's rule integrates with no gates.";
        var said = verdict.Action == VerdictActions.Land
            ? $"landed on the confirm run's pass of {string.Join(", ", verdict.PassedOnConfirm)}, as the owner's rule `unconfirmed: land` chose, for an unconfirmed failure that is a sighting."
            : "the gates passed.";
        if (verdict.Flaked.Count > 0) said += $" A flake read as a pass: {string.Join(", ", verdict.Flaked)}.";
        if (verdict.Inherited.Count > 0) said += $" {verdict.Inherited.Count} inherited known {(verdict.Inherited.Count == 1 ? "failure" : "failures")}.";
        return said;
    }

    internal static string MovedWords(IntegrationCandidate candidate, string line) =>
        $"`{candidate.Line}` moved under the candidate, from {TextBounds.ShortCommit(candidate.Base, 8)} to {TextBounds.ShortCommit(line, 8)}: nothing was moved, "
        + "and the candidate is formed again from the requests that are left.";

    /// <summary>
    /// A hand back's or a split's end: each member the verdict names handed back with its request closed, the candidate formed again where any
    /// member is left (and ended <c>empty</c> where none is). Null where the verdict names no member of this candidate, which is no verdict.
    /// </summary>
    internal static IntegrationStepEnd? HandedBack(IntegrationStore store, IntegrationVerdict verdict, IReadOnlyList<IntegrationMember> replayed)
    {
        var byBranch = replayed.ToDictionary(each => store.Request(each.Request)!.Branch, StringComparer.Ordinal);
        var named = verdict.HandedBack.Where(each => byBranch.ContainsKey(each.Branch)).ToList();
        if (named.Count == 0 && (verdict.Action == VerdictActions.HandBack || verdict.Retain.All(each => !byBranch.ContainsKey(each)))) return null;

        var back = named.Select(each => (Member: byBranch[each.Branch], Words: HandBackWords(each))).ToList();
        var changes = back.Select(each => new IntegrationMemberChange(each.Member.Request, IntegrationMemberStates.HandedBack) { Said = each.Words, Ends = true }).ToList();
        var handed = back.Select(each => each.Member.Request).ToHashSet(StringComparer.Ordinal);
        var split = verdict.Action == VerdictActions.Split;
        return new IntegrationStepEnd(split ? IntegrationStepOutcomes.Split : IntegrationStepOutcomes.HandedBack)
        {
            Said = (split ? "split: the members left run one at a time. " : "")
                   + (back.Count == 0 ? "No member is handed back. " : string.Join(" ", back.Select(each => $"request {each.Member.Request}: {each.Words}")) + " ")
                   + "The members kept are formed again.",
            Members = changes,
            Closes = [.. handed.Select(each => new IntegrationRequestClose(each, IntegrationRequestCloses.HandedBack))],
            Ends = replayed.All(each => handed.Contains(each.Request)) ? IntegrationCandidateEnds.Empty : IntegrationCandidateEnds.FormedAgain,
        };
    }

    private static string HandBackWords(VerdictHandedBack each) =>
        $"handed back ({each.How}) for {each.Failure}"
        + (each.With.Count > 0 ? $", with {string.Join(", ", each.With)}" : "")
        + (each.Interacting.Count > 0 ? $", interacting with {string.Join(", ", each.Interacting)}" : "")
        + (string.IsNullOrWhiteSpace(each.Why) ? "." : $": {each.Why}");

    /// <summary>
    /// The promote step's end where authority lapsed: nothing was moved; each lapsed member is held (its request stays open and out of the
    /// queue, for the steward's or the owner's act) and the candidate is formed again without it, or ended <c>empty</c> where none is left.
    /// </summary>
    internal static IntegrationStepEnd LapsedEnd(IReadOnlyList<IntegrationMember> replayed, IReadOnlyList<(IntegrationMember Member, string Why)> lapsed) =>
        new(IntegrationStepOutcomes.Done)
        {
            Said = string.Join(" ", lapsed.Select(each => $"request {each.Member.Request}: held at promotion, its requester's authority no longer stands ({each.Why}); nothing was moved.")
                .Append(lapsed.Count == replayed.Count
                    ? "No member is left to promote."
                    : "The candidate is formed again without it.")),
            Members =
            [
                .. lapsed.Select(each => new IntegrationMemberChange(each.Member.Request, IntegrationMemberStates.Held)
                {
                    Said = $"held at promotion: its requester's authority no longer stands ({each.Why}); it waits for the steward's or the owner's act.",
                }),
            ],
            Ends = lapsed.Count == replayed.Count ? IntegrationCandidateEnds.Empty : IntegrationCandidateEnds.FormedAgain,
        };

    /// <summary>The integration note, from the record alone: a code and its facts (the line's move, each member's end, the pending dispositions).</summary>
    internal static string NoteOf(IntegrationStore store, string candidate)
    {
        var found = store.Candidate(candidate)!;
        var members = store.Members(candidate);
        var parts = members.Select(each =>
        {
            var request = store.Request(each.Request)!;
            var pending = store.Dispositions(each.Request).Count(disposition => disposition.Pending);
            return $"request {each.Request} (`{request.Branch}`) {each.State}" + (pending > 0 ? $", {pending} superseded {(pending == 1 ? "commit" : "commits")} pending a disposition" : "");
        });
        return $"settled: `{found.Line}` moved from {TextBounds.ShortCommit(found.Base, 8)} to {TextBounds.ShortCommit(found.Tip ?? found.Base, 8)}; {string.Join("; ", parts)}.";
    }
}
